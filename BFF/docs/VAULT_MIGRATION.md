# Переход на Vault — инвентаризация и решения

По [ТЗ tf-bff получает секреты из Vault при старте контейнера]. Раздел 5.1 просит таблицу
«переменная → секрет/настройка → откуда после миграции» — она ниже. Код приложения не менялся (как и
требует ТЗ) — только `Dockerfile`, `docker-compose.yml`/`docker-compose.migrations.yml`, workflow'ы
выкатки, и создан `vault-entrypoint.sh`.

## Инвентаризация

| Переменная | Секрет/настройка | Откуда после миграции |
| --- | --- | --- |
| `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_SCHEMA`, `DB_MAX_POOL_SIZE` | настройка | как раньше, `deploy/.env` |
| `DB_USER` | настройка (имя пользователя, не пароль) | как раньше, `deploy/.env` — `bff_user` |
| `DB_PASSWORD` | **секрет** | Vault: `secret/tf/postgres/bff` → ключ `TF_PG_BFF_USER_PASSWORD` |
| `DB_MIGRATION_USER` | настройка | как раньше, `deploy/migration/.env` — `bff_admin` |
| `DB_MIGRATION_PASSWORD` | **секрет** | Vault: `secret/tf/postgres/bff` → ключ `TF_PG_BFF_ADMIN_PASSWORD` |
| `DB_SKIP_SCHEMA_CREATE` | настройка (флаг) | как раньше, `deploy/migration/.env` |
| `AUTH_SERVICE_URL`, `AUTH_REFRESH_PATH`, `AUTH_REFRESH_TIMEOUT_SECONDS` | настройка | как раньше, `deploy/.env` |
| `AUTH_JWKS_URL` | настройка (это URL, не ключ — сам JWT-ключ приходит по этому URL как открытый PEM-ключ, см. `docs/DECISIONS.md`) | как раньше, `deploy/.env` |
| `AUTH_JWKS_CACHE_MINUTES`, `AUTH_ISSUER`, `AUTH_AUDIENCE`, `AUTH_ACCESS_TOKEN_COOKIE`, `AUTH_USER_ID_CLAIM` | настройка | как раньше, `deploy/.env` |
| `PERMISSIONS_CACHE_TTL_SECONDS`, `RBAC_VERSION_POLL_SECONDS`, `CORS_ALLOWED_ORIGINS`, `LOG_LEVEL`, `DOCKER_NETWORK` | настройка | как раньше, `deploy/.env` |
| `VAULT_ROLE_ID`, `VAULT_SECRET_ID` | доступ к Vault (не секрет сервиса, а ключ к остальным секретам) | GitHub Secrets, Environment `dev`/`prod` — не хранятся в `.env` на сервере вообще |

**Секретов у сервиса ровно два, и оба — пароль Postgres** (для приложения и для миграций отдельно).
Больше в текущем коде BFF ничего секретного нет:

- JWT: BFF только проверяет подпись входящего токена по публичному PEM-ключу, который сам получает по
  `AUTH_JWKS_URL` (см. `docs/DECISIONS.md`, «AUTH_JWKS_URL отдаёт PEM-ключ»). Своего JWT-секрета/приватного
  ключа на стороне BFF нет — нечего класть в `app/tf-bff`.
- Kafka, RabbitMQ, Redis: в коде сервиса ни один из этих клиентов не подключён (см. артефакт
  «Межсервисная модель данных think-front» — Kafka-топики `tf.dispatch.decisions`/`tf.dispatch.settings`
  из §9 исходного домен-документа помечены как «не заведены», без клиента с любой стороны). Пути
  `kafka/bff`, `rabbit/bff`, `redis` из раздела 3 ТЗ намеренно **не добавлены** в `VAULT_SECRET_PATHS` —
  ТЗ прямо разрешает убирать путь, если сервис им не пользуется. Как только появится реальный клиент —
  добавить путь в `VAULT_SECRET_PATHS` обоих compose-файлов и, если он реально несёт секрет, строку в
  эту таблицу.
- `app/tf-bff` (раздел 4 ТЗ, собственные секреты) — тоже не добавлен: нет ни одного секрета для него на
  сегодня (нет SMTP, токенов ботов, внешних API-ключей). Появится такой секрет — путь добавляется сюда
  же, по инструкции раздела 4 ТЗ (список имя→назначение передаётся администратору без значений).

## Как секрет доходит до приложения

Код читает `DB_PASSWORD`/`DB_MIGRATION_PASSWORD` в точности как раньше — это не поменялось. Меняется
только то, что заполняет эту переменную:

```
docker-compose.yml:  DB_PASSWORD: "$${TF_PG_BFF_USER_PASSWORD}"   (буквальная строка ${...} в контейнере)
                              │
                              ▼
vault-entrypoint.sh:  VAULT_EXPAND=DB_PASSWORD
                      → читает secret/tf/postgres/bff, получает TF_PG_BFF_USER_PASSWORD
                      → подставляет его внутрь значения DB_PASSWORD, экспортирует
                              │
                              ▼
                      exec dotnet BFF.WebApi.dll   (видит готовый DB_PASSWORD)
```

Тот же приём для контура миграций: `DB_MIGRATION_PASSWORD` ← `TF_PG_BFF_ADMIN_PASSWORD`.

## Что изменилось в файлах

- **`vault-entrypoint.sh`** (новый, корень `BFF/` — контекст сборки обоих Dockerfile) — скопирован из
  приложения к ТЗ без изменений.
- **`.gitattributes`** (новый, корень репозитория) — `*.sh text eol=lf`, чтобы Windows-чекаут не сломал
  переносы строк в шелл-скрипте.
- **`deploy/Dockerfile`** — `apk add bash curl jq`, копирование и `chmod +x` скрипта,
  `ENTRYPOINT ["/usr/local/bin/vault-entrypoint.sh"]` + `CMD ["dotnet", "BFF.WebApi.dll"]` (было —
  `ENTRYPOINT ["dotnet", "BFF.WebApi.dll"]`).
- **`deploy/docker-compose.yml`** — блок `VAULT_ADDR`/`VAULT_ROLE_ID`/`VAULT_SECRET_ID`/
  `VAULT_SECRET_PATHS=postgres/bff`/`VAULT_EXPAND=DB_PASSWORD` + `DB_PASSWORD: "$${TF_PG_BFF_USER_PASSWORD}"`.
- **`deploy/.env.example`** — `DB_PASSWORD` убран (комментарий вместо значения), `DB_USER`/`DB_NAME`
  приведены к реальным именам (`bff_user`/`tf`, раздел 5.6 ТЗ), примечание про `VAULT_ROLE_ID`/
  `VAULT_SECRET_ID`.
- **`deploy/migration/Dockerfile.migrations`** — `curl jq` в `apt-get install` (рядом с уже бывшим
  `postgresql-client`), тот же `vault-entrypoint.sh`, `ENTRYPOINT`/`CMD` теперь оборачивают существующий
  `/entrypoint.sh` вместо прямого запуска.
- **`deploy/migration/docker-compose.migrations.yml`** — тот же блок `VAULT_*`, но
  `VAULT_EXPAND=DB_MIGRATION_PASSWORD` и `secret ...ADMIN_PASSWORD`.
- **`deploy/migration/.env.example`** — `DB_MIGRATION_PASSWORD` убран, `DB_MIGRATION_USER` переименован
  в `bff_admin` (раздел 5.6 ТЗ), `DB_NAME=tf`.
- **`.github/workflows/deploy-dev.yml`** — `environment: dev` на джобе, `VAULT_ROLE_ID`/`VAULT_SECRET_ID`
  из `secrets.*` прокинуты в шаг SSH и дальше в удалённую сессию через `envs:` (по образцу того, как уже
  прокидывался `TARGET_BRANCH` в `run-bff-migrations.yml`).
- **`.github/workflows/run-bff-migrations.yml`** — то же самое.

## Что нужно от администратора (раздел 8 ТЗ) — только имена, без значений

1. Завести AppRole `tf-svc-tf-bff` с политикой на чтение `secret/tf/postgres/bff` (и позже —
   `kafka/bff`/`rabbit/bff`/`redis`/`app/tf-bff`, когда они понадобятся).
2. Завести в этом пути на каждом стенде два ключа: `TF_PG_BFF_USER_PASSWORD` (пароль `bff_user`) и
   `TF_PG_BFF_ADMIN_PASSWORD` (пароль `bff_admin`).
3. В GitHub-репозитории завести Environments `dev` и `prod`, в каждом — секреты `VAULT_ROLE_ID`,
   `VAULT_SECRET_ID` для соответствующего AppRole/стенда.
4. Собственных секретов сервиса (раздел 4 ТЗ, `app/tf-bff`) на сегодня передавать не нужно — их нет.
5. После первой успешной выкатки на dev с этой схемой — можно удалить старый `DB_PASSWORD`/
   `DB_MIGRATION_PASSWORD` из `.env` на сервере и любые старые пароли из GitHub Secrets, если они там
   лежали раньше.

## Критерии приёмки (раздел 6 ТЗ) — как выполняются

1. Действующих секретов в репозитории не было и нет (`.env` всегда в `.gitignore`, коммитились только
   `.env.example` с плейсхолдерами) — ничего менять по истории git не нужно.
2. В GitHub Secrets теперь нужны только `VAULT_ROLE_ID`/`VAULT_SECRET_ID` (в Environments `dev`/`prod`)
   — старые `DB_PASSWORD`/`DB_MIGRATION_PASSWORD`, если лежали как секреты выкатки, можно удалить после
   переезда.
3–7. Проверяются на стенде при первой выкатке этой ветки — не проверено агентом (Vault недоступен из
   среды разработки, да и запускать что-либо агенту не положено — см. `docs/DECISIONS.md`).
