-- ============================================================
-- Файл:        001_seed_initial_data.sql
-- Назначение:  единая точка начальных данных RBAC — системная
--              группа admins, ВСЕ коды ресурсов, которые знает
--              текущая версия кода (базовые + доменные), и выдача
--              admins полного доступа на каждый из них.
-- Проект:      BFF (think-front)
-- Автор:       sfobosde
-- Дата:        2026-09-25
-- Выполнять:   вручную, после применения всех миграций. Скрипт
--              идемпотентен (ON CONFLICT DO NOTHING/DO UPDATE) —
--              безопасно гонять повторно при переезде на новое
--              окружение или после добавления новых ресурсов в
--              этот же файл, не нужно помнить порядок нескольких
--              отдельных скриптов.
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
    ('model_settings',   'Настройки модели (админ-панель)')
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

COMMIT;

-- Первого администратора добавить вручную:
-- INSERT INTO group_members (group_id, member_type, member_id, created_at)
-- SELECT g.id, 1, '<user-uuid>', now() FROM groups g WHERE g.code = 'admins';
