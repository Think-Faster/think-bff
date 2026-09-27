-- ============================================================
-- Файл:        001_seed_initial_data.sql
-- Назначение:  единая точка начальных данных RBAC — системная
--              группа admins, ВСЕ коды ресурсов, которые знает
--              текущая версия кода (базовые + доменные), выдача
--              admins полного доступа на каждый из них, и
--              плейсхолдер-профиль первого администратора (только
--              на действительно новом стенде — см. шаг 6).
-- Проект:      BFF (think-front)
-- Автор:       sfobosde
-- Дата:        2026-09-25
-- Выполнять:   автоматически, контуром миграций (deploy/migration/entrypoint.sh) —
--              сразу после dotnet ef database update, на каждом деплое. Можно и
--              вручную (см. Запуск) — идемпотентен (ON CONFLICT DO NOTHING/DO
--              UPDATE, шаг 6 — по количеству участников admins), безопасно гонять
--              повторно при переезде на новое окружение или после добавления
--              новых ресурсов в этот же файл.
-- Запуск:      psql -f 001_seed_initial_data.sql (или любой другой SQL-клиент —
--              см. примечание про SEARCH_PATH ниже).
-- ВНИМАНИЕ:    скрипт не выполнялся при разработке.
-- ПРИМЕЧАНИЕ:  gen_random_uuid() требует расширения pgcrypto на
--              PostgreSQL < 13 (подключить: CREATE EXTENSION IF
--              NOT EXISTS pgcrypto;). На PostgreSQL 13+ функция
--              встроена и расширение не требуется.
-- ПРИМЕЧАНИЕ:  ниже — обычный SQL, без psql-специфичных команд (`\set`, `:var`) —
--              они распознаются только настоящим интерактивным клиентом psql;
--              через GUI-клиенты, docker exec с другим раннером и т.п. они
--              улетают на сервер как есть и падают с ошибкой синтаксиса.
--              Схема захардкожена ниже как "bff" — поменяй на свою (DB_SCHEMA),
--              если она называется иначе.
--
-- Заменяет собой 001_seed_admins_group.sql + 002_seed_domain_resources.sql
-- (их содержимое объединено сюда без изменений по существу).
-- ============================================================

SET search_path TO bff;

BEGIN;

-- 1. Системная группа администраторов
INSERT INTO groups (id, code, name, is_system, created_at, updated_at)
VALUES (gen_random_uuid(), 'admins', 'Администраторы', true, now(), now())
ON CONFLICT (code) DO NOTHING;

-- 2. Все коды ресурсов, известные текущей версии кода (BFF.Models.Constants.ResourceCodes).
-- Новую сущность добавляешь — новую строку сюда же, а не отдельным файлом (раздел 6
-- docs/ADDING_NEW_ENTITY.md); ON CONFLICT DO NOTHING делает повторный прогон безопасным.
INSERT INTO resources (code, name) VALUES
    -- базовые (D6 — RBAC-ядро)
    ('users',            'Пользователи'),
    ('groups',           'Группы'),
    ('permissions',      'Права доступа'),
    -- доменные (D1/D3/D4/D6-доп/D8)
    ('objects',          'Объекты и топология'),
    ('sensors',          'Датчики'),
    ('predictions',      'Прогнозы'),
    ('tasks',            'Заявки и работы'),
    ('incidents',        'Происшествия'),
    ('schedule',         'График работы'),
    ('assigned_objects', 'Закреплённые объекты'),
    ('engineers',        'Инженеры и бригады'),
    ('presence',         'Присутствие'),
    ('model_settings',   'Настройки модели (админ-панель)'),
    ('notifications',    'Email-рассылки')
ON CONFLICT (code) DO NOTHING;

-- 3. Группе admins — полные права (маска 127 = все биты, включая manage) на КАЖДЫЙ
-- зарегистрированный ресурс, включая те, что появятся выше в будущем.
INSERT INTO access_grants
    (id, principal_type, principal_id, resource_id, permission_mask, created_at, updated_at)
SELECT gen_random_uuid(), 2, g.id, r.id, 127, now(), now()
FROM groups g
CROSS JOIN resources r
WHERE g.code = 'admins'
ON CONFLICT (principal_type, principal_id, resource_id)
DO UPDATE SET permission_mask = 127, updated_at = now();

-- 4. Рефлексивная строка замыкания для новой группы
-- Обязательна: без неё запрос эффективных прав (раздел 6.4/7.2) не найдёт гранты группы admins,
-- пока не произойдёт первая мутация графа групп и не будет пересчитано group_closure целиком
-- (см. также комментарий в GroupService.CreateAsync, который делает то же самое для новых групп).
INSERT INTO group_closure (ancestor_id, descendant_id, depth)
SELECT g.id, g.id, 0 FROM groups g WHERE g.code = 'admins'
ON CONFLICT (ancestor_id, descendant_id) DO NOTHING;

-- 5. Сброс кэша прав приложения
INSERT INTO rbac_version (id, value) VALUES (1, 1)
ON CONFLICT (id) DO UPDATE SET value = rbac_version.value + 1;

-- 6. Плейсхолдер-профиль первого администратора — только если в группе admins ещё
-- совсем никого нет. Идемпотентность здесь проверяется по количеству участников
-- группы, а НЕ по auth_user_id: после того как заглушку заменят на настоящий
-- auth_user_id (см. ниже), строки с этим auth_user_id уже не будет, и наивная
-- проверка ON CONFLICT (auth_user_id) DO NOTHING создала бы плейсхолдер заново на
-- следующем деплое. Проверка по "участников ещё нет" такого не допускает: как
-- только в admins появился хоть кто-то (этот плейсхолдер или настоящий человек),
-- шаг больше никогда не сработает повторно.
WITH admins_group AS (
    SELECT id FROM groups WHERE code = 'admins'
),
new_admin AS (
    INSERT INTO users (id, auth_user_id, first_name, last_name, is_active, created_at, updated_at)
    SELECT gen_random_uuid(), '__bootstrap_admin__', 'Bootstrap', 'Admin', true, now(), now()
    WHERE NOT EXISTS (
        SELECT 1 FROM group_members gm JOIN admins_group ag ON gm.group_id = ag.id
    )
    RETURNING id
)
INSERT INTO group_members (group_id, member_type, member_id, created_at)
SELECT ag.id, 1, new_admin.id, now()
FROM admins_group ag, new_admin;

COMMIT;

-- После первого прогона на новом стенде: заменить заглушку auth_user_id у профиля
-- на настоящий sub из auth (саму строку не удалять — иначе следующий деплой снова
-- решит, что в admins никого нет, и создаст плейсхолдер заново):
--   UPDATE users SET auth_user_id = '<sub из auth>', updated_at = now()
--   WHERE auth_user_id = '__bootstrap_admin__';
