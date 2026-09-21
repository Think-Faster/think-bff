-- ============================================================
-- Файл:        001_seed_admins_group.sql
-- Назначение:  создание системной группы admins, регистрация
--              базовых ресурсов и выдача группе полных прав.
-- Проект:      BFF (think-front)
-- Автор:       sfobosde
-- Дата:        2026-09-21
-- Выполнять:   вручную, ОДИН РАЗ, после применения миграций.
-- Запуск:      psql -v schema=bff -f 001_seed_admins_group.sql
-- ВНИМАНИЕ:    скрипт не выполнялся при разработке.
-- ПРИМЕЧАНИЕ:  gen_random_uuid() требует расширения pgcrypto на
--              PostgreSQL < 13 (подключить: CREATE EXTENSION IF
--              NOT EXISTS pgcrypto;). На PostgreSQL 13+ функция
--              встроена и расширение не требуется.
-- ============================================================

\set ON_ERROR_STOP on
SET search_path TO :schema;

BEGIN;

-- 1. Системная группа администраторов
INSERT INTO groups (id, code, name, is_system, created_at, updated_at)
VALUES (gen_random_uuid(), 'admins', 'Администраторы', true, now(), now())
ON CONFLICT (code) DO NOTHING;

-- 2. Базовые защищаемые ресурсы
INSERT INTO resources (code, name) VALUES
    ('users',       'Пользователи'),
    ('groups',      'Группы'),
    ('permissions', 'Права доступа')
ON CONFLICT (code) DO NOTHING;

-- 3. Полные права (маска 127 = все биты, включая manage)
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

-- 5. Сброс кэша прав
INSERT INTO rbac_version (id, value) VALUES (1, 1)
ON CONFLICT (id) DO UPDATE SET value = rbac_version.value + 1;

COMMIT;

-- Первого администратора добавить вручную:
-- INSERT INTO group_members (group_id, member_type, member_id, created_at)
-- SELECT g.id, 1, '<user-uuid>', now() FROM groups g WHERE g.code = 'admins';
