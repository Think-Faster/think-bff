#!/bin/sh
# Applies EF Core migrations to an already-running Postgres instance. NOT executed by the agent that
# generated this repository (section 1, hard constraint #3/#4) — this only runs when a human starts the
# bff-migrations container by hand, after the database exists (section 12.3).
set -eu

: "${DB_HOST:?DB_HOST is required}"
: "${DB_PORT:=5432}"
: "${DB_NAME:?DB_NAME is required}"
: "${DB_SCHEMA:?DB_SCHEMA is required}"
: "${DB_MIGRATION_USER:?DB_MIGRATION_USER is required}"
: "${DB_MIGRATION_PASSWORD:?DB_MIGRATION_PASSWORD is required}"

echo "Ensuring schema \"${DB_SCHEMA}\" exists on ${DB_HOST}:${DB_PORT}/${DB_NAME}..."
PGPASSWORD="${DB_MIGRATION_PASSWORD}" psql \
  --host "${DB_HOST}" --port "${DB_PORT}" --dbname "${DB_NAME}" --username "${DB_MIGRATION_USER}" \
  --set ON_ERROR_STOP=on \
  --command "CREATE SCHEMA IF NOT EXISTS \"${DB_SCHEMA}\";"

echo "Applying migrations..."
export DB_CONNECTION_STRING="Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_MIGRATION_USER};Password=${DB_MIGRATION_PASSWORD};Search Path=${DB_SCHEMA}"

dotnet ef database update \
  --project src/BFF.Context \
  --startup-project src/BFF.WebApi
