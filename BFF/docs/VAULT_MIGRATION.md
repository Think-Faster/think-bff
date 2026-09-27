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
| `DB_SKIP_SCHEMA_CREATE` | настройка (флаг), но зафиксирована | зашита `"true"` прямо в `docker-compose.migrations.yml` — не в `.env` (архитектурный факт: инфра владеет схемой, не настройка стенда) |
| `AUTH_SERVICE_URL`, `AUTH_REFRESH_PATH`, `AUTH_REFRESH_TIMEOUT_SECONDS` | настройка | как раньше, `deploy/.env` |
| `AUTH_JWKS_URL` | настройка (это URL, не ключ — сам JWT-ключ приходит по этому URL как открытый PEM-ключ, см. `docs/DECISIONS.md`) | как раньше, `deploy/.env` |
| `AUTH_JWKS_CACHE_MINUTES`, `AUTH_ISSUER`, `AUTH_AUDIENCE`, `AUTH_ACCESS_TOKEN_COOKIE`, `AUTH_USER_ID_CLAIM` | настройка | как раньше, `deploy/.env` |
| `PERMISSIONS_CACHE_TTL_SECONDS`, `RBAC_VERSION_POLL_SECONDS`, `CORS_ALLOWED_ORIGINS`, `LOG_LEVEL` | настройка | как раньше, `deploy/.env` |
| `REDIS_HOST` | настройка (не секрет — просто адрес) | как раньше, зашито в `docker-compose.yml` (`tf-redis:6379`) |
| `TF_REDIS_PASSWORD` | **секрет** | Vault: `secret/tf/redis` → ключ `TF_REDIS_PASSWORD` (код читает его напрямую по этому имени, без `VAULT_EXPAND` — см. `AuditWriter.cs`) |
| `RABBIT_HOST`, `RABBIT_PORT`, `RABBIT_VHOST`, `RABBIT_USER` | настройка (не секреты, есть дефолты в коде: `tf-rabbit`/`5672`/`tf`/`tf-bff`) | как раньше, `deploy/.env` (можно не задавать вовсе) |
| `TF_RABBIT_BFF_PASSWORD` | **секрет** | Vault: `secret/tf/rabbit/bff` → ключ `TF_RABBIT_BFF_PASSWORD` (код читает напрямую по имени, без `VAULT_EXPAND` — см. `RabbitMqNoticePublisher.cs`) |
| `VAULT_ROLE_ID`, `VAULT_SECRET_ID` | доступ к Vault (не секрет сервиса, а ключ к остальным секретам) | GitHub Secrets, Environment `dev`/`prod` — не хранятся в `.env` на сервере вообще |

Сети больше нет в этом списке: раньше `DOCKER_NETWORK` была настраиваемой переменной (`deploy/.env`),
теперь сеть одна и зашита буквально как `think-fast-net` в обоих compose-файлах — см. «Сеть: одна,
`think-fast-net`» ниже.

**Секретов у сервиса четыре: два пароля Postgres, пароль Redis, пароль RabbitMQ.** Больше в текущем коде
BFF ничего секретного нет:

- JWT: BFF только проверяет подпись входящего токена по публичному PEM-ключу, который сам получает по
  `AUTH_JWKS_URL` (см. `docs/DECISIONS.md`, «AUTH_JWKS_URL отдаёт PEM-ключ»). Своего JWT-секрета/приватного
  ключа на стороне BFF нет — нечего класть в `app/tf-bff`.
- Redis подключён (журнал действий — `AuditWriter`, поток Redis `audit`, см. `docs/DECISIONS.md` про
  Н16; плюс антиспам-лимитер email-рассылки, `EmailRateLimiter`) — путь `secret/tf/redis` заведён в
  `VAULT_SECRET_PATHS` у `tf-bff`.
- RabbitMQ подключён (публикация email-уведомлений в `tf.notifications` для `tf-mail` — задание
  инфраструктуры, см. `docs/DECISIONS.md`, «Пересмотр после ТЗ инфраструктуры: письма через RabbitMQ, не
  SMTP») — путь `secret/tf/rabbit/bff` заведён в `VAULT_SECRET_PATHS` у `tf-bff`. Только публикация в
  `tf.notifications`/`tf.model.commands` (второе пока не используется), без права объявлять
  exchange/очереди.
- Kafka: в коде сервиса до сих пор не подключён (см. артефакт «Межсервисная модель данных think-front» —
  Kafka-топики `tf.dispatch.decisions`/`tf.dispatch.settings` из §9 исходного домен-документа помечены
  как «не заведены», без клиента с любой стороны). Путь `kafka/bff` намеренно **не добавлен** в
  `VAULT_SECRET_PATHS` — тот же принцип, что раньше применялся и к RabbitMQ: заводить путь только когда
  есть реальный клиент.
- `app/tf-bff` (раздел 4 ТЗ, собственные секреты) — тоже не добавлен: нет ни одного секрета для него на
  сегодня (нет токенов ботов, внешних API-ключей). Появится такой секрет — путь добавляется сюда же, по
  инструкции раздела 4 ТЗ (список имя→назначение передаётся администратору без значений).

## Сеть: одна, `think-fast-net`

Инфраструктура объединила все сети стенда в одну — `think-fast-net`: Postgres (`tf-postgres`), Vault,
Kafka/RabbitMQ/Redis (когда понадобятся) и nginx теперь все в ней. Раньше было две сети
(`app-network`/`postgree_app-network`), и имя первой бралось из переменной `DOCKER_NETWORK` в `.env` —
это уже приводило к реальному инциденту: `deploy/migration/.env` на сервере указывал сеть
Postgres (`postgree_app-network`) вместо сети Vault, из-за чего `bff-migrations` не мог достать
секреты (10 минут ретраев логина в Vault, см. `docs/DECISIONS.md`). Теперь сеть одна и зашита в оба
compose-файла буквально, без переменной, — рассинхрон такого рода структурно не может повториться.

## Хосты Postgres/Kafka/RabbitMQ/Redis — новые имена

Инфраструктура стенда переехала на новую схему (новая база, новые пароли). Хост Postgres теперь
`tf-postgres` (было `postgres`), Kafka — `tf-kafka`, RabbitMQ — `tf-rabbit`, Redis — `tf-redis`
(последний уже был так назван, когда добавляли аудит). Обновлены `DB_HOST` в обоих `.env.example` и в
`scripts/seed-via-vault.sh`.

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

1. Завести AppRole `tf-svc-tf-bff` с политикой на чтение `secret/tf/postgres/bff`, `secret/tf/redis` и
   `secret/tf/rabbit/bff` (и позже — `kafka/bff`/`app/tf-bff`, когда они понадобятся). Про `rabbit/bff`
   инфраструктура сообщила, что уже настроено (пароль в Vault, права `tf-bff` на публикацию в
   `tf.notifications`/`tf.model.commands`) — проверить, что реально доступно этому AppRole, а не
   заводить с нуля.
2. Завести ключи: в `secret/tf/postgres/bff` — `TF_PG_BFF_USER_PASSWORD` (пароль `bff_user`) и
   `TF_PG_BFF_ADMIN_PASSWORD` (пароль `bff_admin`); в `secret/tf/redis` — `TF_REDIS_PASSWORD`; в
   `secret/tf/rabbit/bff` — `TF_RABBIT_BFF_PASSWORD` (по заявлению инфраструктуры — уже заведён).
3. В GitHub-репозитории завести Environments `dev` и `prod`, в каждом — секреты `VAULT_ROLE_ID`,
   `VAULT_SECRET_ID` для соответствующего AppRole/стенда.
4. Собственных секретов сервиса (раздел 4 ТЗ, `app/tf-bff`) на сегодня передавать не нужно — их нет.
5. После первой успешной выкатки на dev с этой схемой — можно удалить старый `DB_PASSWORD`/
   `DB_MIGRATION_PASSWORD` из `.env` на сервере и любые старые пароли из GitHub Secrets, если они там
   лежали раньше.

## Разбор реального сбоя миграций на dev и его причины

При первой попытке применить схему на пересозданной инфраструктуре (новая база, новые пароли, новая
топология сети) миграции падали с `password authentication failed for user "tf"`. Разбор:

- `tf` — не «имя базы по ошибке», а реальная роль-суперпользователь Postgres, которой инфраструктура
  создаёт схему `bff`, роли `bff_admin`/`bff_user` и права (раздел 5 инфра-ТЗ) — сервису `tf-bff` она
  недоступна и не должна быть доступна ни для миграций, ни для приложения. Ошибка означала, что где-то
  в конфиге на сервере (`deploy/migration/.env`) `DB_MIGRATION_USER` был не тем, что в шаблоне репозитория
  (`bff_admin`) — устаревшее значение с прошлой итерации стенда.
- Отдельно — сетевая часть (см. «Сеть: одна, `think-fast-net`» выше): `DOCKER_NETWORK` в
  `deploy/migration/.env` указывал на сеть Postgres, а не на сеть Vault, из-за чего `bff-migrations` не
  мог даже войти в Vault за паролем — до проверки самого пользователя дело не доходило. Оба фактора
  разбирались по отдельности (см. `docs/DECISIONS.md`), в этой ревизии убраны структурно: сеть теперь не
  настраивается (`think-fast-net` зашит буквально), а `DB_MIGRATION_USER`/`DB_HOST` в шаблонах —
  `bff_admin`/`tf-postgres`, актуальные значения на сервере нужно свести к этим же.
- Третий шаг (после того как сеть и пользователь были поправлены): `permission denied for database tf`
  на `CREATE SCHEMA IF NOT EXISTS "bff"` — `bff_admin` владеет схемой, но не имеет `CREATE` на самой базе
  (архитектурно, по решению инфраструктуры). `DB_SKIP_SCHEMA_CREATE` в `.env` на сервере ещё оставался
  `false` (значение с прошлой итерации, до того как инфра однозначно взяла на себя владение схемой).
  Зафиксировали `"true"` прямо в `docker-compose.migrations.yml` (см. таблицу выше) — теперь это не
  зависит от `.env` вообще.

## Критерии приёмки (раздел 6 ТЗ) — как выполняются

1. Действующих секретов в репозитории не было и нет (`.env` всегда в `.gitignore`, коммитились только
   `.env.example` с плейсхолдерами) — ничего менять по истории git не нужно.
2. В GitHub Secrets теперь нужны только `VAULT_ROLE_ID`/`VAULT_SECRET_ID` (в Environments `dev`/`prod`)
   — старые `DB_PASSWORD`/`DB_MIGRATION_PASSWORD`, если лежали как секреты выкатки, можно удалить после
   переезда.
3–7. Проверяются на стенде при первой выкатке этой ветки — не проверено агентом (Vault недоступен из
   среды разработки, да и запускать что-либо агенту не положено — см. `docs/DECISIONS.md`).
