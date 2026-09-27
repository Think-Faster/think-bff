#!/bin/sh
# Applies EF Core migrations, then seeds initial RBAC data (scripts/001_seed_initial_data.sql), to an
# already-running Postgres instance. Runs automatically on every deploy (deploy-dev.yml, before tf-bff
# starts) and can also be started by hand — see README.md "Миграции"/"Начальные данные". NOT executed by
# the agent that generated this repository (section 1, hard constraint #3/#4).
set -eu

: "${DB_HOST:?DB_HOST is required}"
: "${DB_PORT:=5432}"
: "${DB_NAME:?DB_NAME is required}"
: "${DB_SCHEMA:?DB_SCHEMA is required}"
: "${DB_MIGRATION_USER:?DB_MIGRATION_USER is required}"
: "${DB_MIGRATION_PASSWORD:?DB_MIGRATION_PASSWORD is required}"
: "${DB_SKIP_SCHEMA_CREATE:=false}"

echo "--- bff-migrations: input parameters (secrets omitted) ---"
echo "DB_HOST               = ${DB_HOST}"
echo "DB_PORT               = ${DB_PORT}"
echo "DB_NAME               = ${DB_NAME}"
echo "DB_SCHEMA             = ${DB_SCHEMA}"
echo "DB_MIGRATION_USER     = ${DB_MIGRATION_USER}"
echo "DB_MIGRATION_PASSWORD = [${#DB_MIGRATION_PASSWORD} chars, hidden]"
echo "DB_SKIP_SCHEMA_CREATE = ${DB_SKIP_SCHEMA_CREATE}"
echo "-----------------------------------------------------------"

echo "Checking that ${DB_MIGRATION_USER} can connect to ${DB_HOST}:${DB_PORT}/${DB_NAME}..."
if ! PGPASSWORD="${DB_MIGRATION_PASSWORD}" psql \
  --host "${DB_HOST}" --port "${DB_PORT}" --dbname "${DB_NAME}" --username "${DB_MIGRATION_USER}" \
  --set ON_ERROR_STOP=on --tuples-only --command "SELECT 1;" >/dev/null; then
  echo "Cannot even CONNECT to database \"${DB_NAME}\" as \"${DB_MIGRATION_USER}\"." >&2
  echo "This is broader than missing CREATE — check pg_hba.conf / GRANT CONNECT ON DATABASE \"${DB_NAME}\" TO ${DB_MIGRATION_USER}, and that the role/password are correct." >&2
  exit 1
fi
echo "Connection OK."

# CREATE SCHEMA IF NOT EXISTS still requires CREATE on the database to even attempt the statement — the
# permission check runs before the existence check, so IF NOT EXISTS does not make this a no-op for a
# user without that right. When the schema is already provisioned by infra ahead of time (and
# DB_MIGRATION_USER intentionally has no CREATE on the database, only rights inside the existing schema),
# set DB_SKIP_SCHEMA_CREATE=true to skip straight to applying migrations.
if [ "${DB_SKIP_SCHEMA_CREATE}" = "true" ]; then
  echo "DB_SKIP_SCHEMA_CREATE=true — assuming schema \"${DB_SCHEMA}\" already exists, skipping creation."
else
  echo "Ensuring schema \"${DB_SCHEMA}\" exists on ${DB_HOST}:${DB_PORT}/${DB_NAME}..."
  PGPASSWORD="${DB_MIGRATION_PASSWORD}" psql \
    --host "${DB_HOST}" --port "${DB_PORT}" --dbname "${DB_NAME}" --username "${DB_MIGRATION_USER}" \
    --set ON_ERROR_STOP=on \
    --command "CREATE SCHEMA IF NOT EXISTS \"${DB_SCHEMA}\";"
fi

echo "Applying migrations..."
export DB_CONNECTION_STRING="Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_MIGRATION_USER};Password=${DB_MIGRATION_PASSWORD};Search Path=${DB_SCHEMA}"

dotnet ef database update \
  --project src/BFF.Context \
  --startup-project src/BFF.WebApi

# Коды ресурсов, группа admins, её права и (только на действительно новом стенде — см. сам файл)
# плейсхолдер-профиль первого администратора. Идемпотентен, безопасно гонять на каждом деплое —
# см. scripts/001_seed_initial_data.sql.
echo "Seeding initial RBAC data..."
PGPASSWORD="${DB_MIGRATION_PASSWORD}" psql \
  --host "${DB_HOST}" --port "${DB_PORT}" --dbname "${DB_NAME}" --username "${DB_MIGRATION_USER}" \
  --set ON_ERROR_STOP=on --file scripts/001_seed_initial_data.sql
