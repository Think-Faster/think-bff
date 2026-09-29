#!/bin/bash
# ============================================================
# seed-via-vault.sh — достаёт пароль БД из Vault и одноразово прогоняет
# 001_seed_initial_data.sql (см. рядом) на целевой базе.
#
# Ручной инструмент для новой/пересозданной инфраструктуры — заменяет
# собой шаги "достать пароль из Vault" + "psql -f ..." из
# docs/VAULT_MIGRATION.md. Не часть выкатки, не запускается автоматически
# ни при старте сервиса, ни в CI.
#
# Переменные окружения:
#   VAULT_ADDR                        адрес Vault, например http://vault:8200
#   VAULT_TOKEN                       человеческий токен (после `vault login`) —
#                                     если задан, используется вместо AppRole
#   VAULT_ROLE_ID / VAULT_SECRET_ID   AppRole сервиса, как альтернатива VAULT_TOKEN
#   DB_HOST      (по умолчанию tf-postgres)
#   DB_PORT      (по умолчанию 5432)
#   DB_NAME      (по умолчанию tf)
#   DB_USER      (по умолчанию bff_user)
#   SECRET_KEY   ключ в secret/tf/postgres/bff (по умолчанию TF_PG_BFF_USER_PASSWORD;
#                для запуска от bff_admin — TF_PG_BFF_ADMIN_PASSWORD)
#
# Примеры:
#   VAULT_ADDR=http://vault:8200 VAULT_TOKEN=hvs.xxx DB_HOST=localhost \
#     ./seed-via-vault.sh
#
#   VAULT_ADDR=http://vault:8200 VAULT_ROLE_ID=... VAULT_SECRET_ID=... \
#     DB_HOST=tf-postgres DB_USER=bff_admin SECRET_KEY=TF_PG_BFF_ADMIN_PASSWORD \
#     ./seed-via-vault.sh
#
# Нужны в PATH: curl, jq, psql.
# ============================================================

set -euo pipefail

log() { echo "[seed-via-vault] $*" >&2; }

: "${VAULT_ADDR:?VAULT_ADDR is not set}"

DB_HOST="${DB_HOST:-tf-postgres}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:-tf}"
DB_USER="${DB_USER:-bff_user}"
SECRET_KEY="${SECRET_KEY:-TF_PG_BFF_USER_PASSWORD}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SEED_FILE="$SCRIPT_DIR/001_seed_initial_data.sql"
[ -f "$SEED_FILE" ] || { log "ERROR: не найден $SEED_FILE"; exit 1; }

own_token=false

if [ -n "${VAULT_TOKEN:-}" ]; then
    token="$VAULT_TOKEN"
    log "используется VAULT_TOKEN"
elif [ -n "${VAULT_ROLE_ID:-}" ] && [ -n "${VAULT_SECRET_ID:-}" ]; then
    log "логин в Vault по AppRole..."
    token="$(jq -nc --arg r "$VAULT_ROLE_ID" --arg s "$VAULT_SECRET_ID" '{role_id: $r, secret_id: $s}' \
        | curl -sSf --max-time 10 -X POST --data @- "$VAULT_ADDR/v1/auth/approle/login" \
        | jq -er '.auth.client_token')"
    own_token=true
else
    log "ERROR: нужен либо VAULT_TOKEN, либо пара VAULT_ROLE_ID/VAULT_SECRET_ID"
    exit 1
fi

cleanup() {
    if [ "$own_token" = true ]; then
        curl -sf --max-time 10 -X POST -H "X-Vault-Token: $token" \
            "$VAULT_ADDR/v1/auth/token/revoke-self" > /dev/null 2>&1 || true
    fi
}
trap cleanup EXIT

log "чтение secret/tf/postgres/bff, ключ $SECRET_KEY..."
encoded="$(curl -sSf --max-time 10 -H "X-Vault-Token: $token" \
    "$VAULT_ADDR/v1/secret/data/tf/postgres/bff" \
    | jq -er --arg k "$SECRET_KEY" '.data.data[$k]')"
password="$(printf '%s' "$encoded" | base64 -d)"

log "запуск $(basename "$SEED_FILE") на $DB_USER@$DB_HOST:$DB_PORT/$DB_NAME..."
PGPASSWORD="$password" psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -v ON_ERROR_STOP=1 -f "$SEED_FILE"

log "готово"
