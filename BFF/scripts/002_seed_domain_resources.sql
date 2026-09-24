-- ============================================================
-- Файл:        002_seed_domain_resources.sql
-- Назначение:  регистрация кодов ресурсов для доменных сущностей
--              D1/D3/D4/D6-доп/D8 и выдача группе admins права
--              manage на все них (по аналогии с 001).
-- Проект:      BFF (think-front)
-- Автор:       sfobosde
-- Дата:        2026-09-24
-- Выполнять:   вручную, ОДИН РАЗ, после применения миграции
--              AddDomainEntities и после 001_seed_admins_group.sql.
-- Запуск:      psql -v schema=bff -f 002_seed_domain_resources.sql
-- ВНИМАНИЕ:    скрипт не выполнялся при разработке.
-- ============================================================

\set ON_ERROR_STOP on
SET search_path TO :schema;

BEGIN;

-- 1. Новые коды ресурсов (см. BFF.Models.Constants.ResourceCodes)
INSERT INTO resources (code, name) VALUES
    ('objects',          'Объекты и топология'),
    ('sensors',          'Датчики'),
    ('predictions',      'Прогнозы'),
    ('tasks',            'Заявки и работы'),
    ('incidents',        'Происшествия'),
    ('schedule',         'График работы'),
    ('assigned_objects', 'Закреплённые объекты'),
    ('engineers',        'Инженеры и бригады'),
    ('model_settings',   'Настройки модели (админ-панель)')
ON CONFLICT (code) DO NOTHING;

-- 2. Группе admins — manage на все новые ресурсы (маска 127)
INSERT INTO access_grants
    (id, principal_type, principal_id, resource_id, permission_mask, created_at, updated_at)
SELECT gen_random_uuid(), 2, g.id, r.id, 127, now(), now()
FROM groups g
CROSS JOIN resources r
WHERE g.code = 'admins'
  AND r.code IN (
      'objects', 'sensors', 'predictions', 'tasks', 'incidents',
      'schedule', 'assigned_objects', 'engineers', 'model_settings'
  )
ON CONFLICT (principal_type, principal_id, resource_id)
DO UPDATE SET permission_mask = 127, updated_at = now();

-- 3. Сброс кэша прав приложения
INSERT INTO rbac_version (id, value) VALUES (1, 1)
ON CONFLICT (id) DO UPDATE SET value = rbac_version.value + 1;

COMMIT;
