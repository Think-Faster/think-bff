# BFF

Backend For Frontend для SPA `think-front`: отдаёт фронтенду доменные модели пользователей/групп и
управляет доступом через RBAC (принципалы + ресурсы + битовая маска прав, раздел 4 ТЗ). Сервис живёт
только во внутренней Docker-сети — порты наружу не публикуются, единственная точка входа снаружи — nginx.

Подробности реализации, принятые по ходу разработки решения и их обоснование — в
[`docs/DECISIONS.md`](docs/DECISIONS.md).

## Стек

.NET 8.0 / ASP.NET Core Web API, EF Core 8 + Npgsql (PostgreSQL 16), FluentValidation, Serilog (JSON в
stdout), Swashbuckle (Swagger UI только при `ASPNETCORE_ENVIRONMENT=Development`).

## Структура решения

```
BFF.sln
src/
  BFF.WebApi/          # точка входа: контроллеры, middleware, авторизация
  BFF.Models/           # доменные сущности и enum'ы, без зависимостей
  BFF.Contracts/        # DTO запросов/ответов API
  BFF.Context/          # EF Core: DbContext, конфигурации, миграции, репозитории
  BFF.Application/       # бизнес-логика, сервисы, валидаторы, DI-композиция
  BFF.Infrastructure/    # клиент сервиса аутентификации, валидация JWT
deploy/                  # Dockerfile, docker-compose.yml, отдельный контур миграций
scripts/                 # 001_seed_initial_data.sql — создать, не выполнять
```

Зависимости строго в одну сторону: `WebApi -> Application, Contracts, Infrastructure`; `Application ->
Context, Contracts, Models`; `Context -> Models`; `Infrastructure -> Models`; `Contracts -> Models`.
`BFF.Models` ни от чего не зависит. `BFF.Context` не знает про HTTP. Контроллеры не обращаются к
`DbContext` напрямую — только через сервисы `BFF.Application`.

## Схема БД через переменную окружения

Ни одна конфигурация EF не задаёт схему явно (`ToTable("users")`, без `HasDefaultSchema`/`schema:`).
Имя схемы приходит через `Search Path` в строке подключения, из `DB_SCHEMA`. Один и тот же образ и одна
и та же миграция разворачивают сервис в любое окружение — меняется только переменная. Подробности —
раздел 5 ТЗ и `docs/DECISIONS.md`.

## Локальная сборка (проверка компилируемости)

```bash
cd BFF
dotnet restore
dotnet build
```

## Переменные окружения

Полный список — раздел 11 ТЗ и `deploy/.env.example` / `deploy/migration/.env.example`. При старте
сервис проверяет обязательные переменные (`DB_HOST`, `DB_NAME`, `DB_USER`, `DB_PASSWORD`, `DB_SCHEMA`,
`AUTH_SERVICE_URL`, `AUTH_JWKS_URL`) и падает с понятной ошибкой, если какой-то не хватает — без
дефолтов на проде. `appsettings.json` в `BFF.WebApi` содержит только локальные dev-дефолты и не хранит
секретов.

`DB_PASSWORD` в `.env` больше не задаётся — секреты приходят из Vault при старте контейнера (см. раздел
«Vault» ниже). Всё остальное в этом списке — обычная конфигурация, лежит в `.env` как раньше.

## Vault

Пароли БД (`DB_PASSWORD`/`DB_MIGRATION_PASSWORD`) не хранятся в `.env` — их читает из Vault
`vault-entrypoint.sh` при старте контейнера (AppRole-логин по `VAULT_ROLE_ID`/`VAULT_SECRET_ID`,
путь `secret/tf/postgres/bff`), экспортирует как обычные переменные окружения и передаёт управление
основному процессу. Без `VAULT_ROLE_ID` скрипт ничего не делает и сразу запускает основной процесс —
локальная разработка с паролем прямо в `.env` продолжает работать без изменений.

`VAULT_ROLE_ID`/`VAULT_SECRET_ID` живут только в GitHub Secrets (Environments `dev`/`prod`) и
прокидываются в контейнер выкаткой (`docker-compose.yml` требует их через `${VAULT_ROLE_ID:?...}`) — в
`.env` на сервере их быть не должно. Полная инвентаризация переменных, обоснование выбранного набора
путей в Vault и что нужно завести администратору — [`docs/VAULT_MIGRATION.md`](docs/VAULT_MIGRATION.md).

Сеть — одна, `think-fast-net` (зашита в обоих compose-файлах буквально, без переменной): в ней сидят
Postgres (`tf-postgres`), Vault, и всё остальное, с чем сервис может взаимодействовать. Отдельной сети
для БД (`postgree_app-network`) больше нет — инфраструктура объединила всё в одну сеть.

## Docker

Запуск сервиса (после того как появится сеть `think-fast-net`, переменные в `deploy/.env`, скопированном
из `deploy/.env.example`, и `vault`/`tf-postgres` доступны в этой сети):

```bash
docker compose -f deploy/docker-compose.yml up -d --build
```

Порты наружу не публикуются — сервис доступен только другим контейнерам в `think-fast-net` (nginx, вероятно
позже — think-front). Контейнер БД в этом compose не описан: база (`tf-postgres`) разворачивается отдельно,
инфраструктурой, в той же сети.

## Миграции

**Не выполнялось при разработке.** Файлы миграции уже сгенерированы (`dotnet ef migrations add`,
единственное разрешённое агенту действие сверх создания файлов — раздел 1 ТЗ) и лежат в
`src/BFF.Context/Migrations`.

На dev-стенде применяются автоматически: `.github/workflows/deploy-dev.yml` перед пересборкой `tf-bff`
сначала собирает и запускает контейнер миграций (`--abort-on-container-exit`) и останавливает выкатку
(`set -e` в скрипте), если он завершился с ошибкой — приложение не поднимется на несовпадающей схеме.
Требует того же `deploy/migration/.env` на сервере, что и раньше (см. ниже). Ручной запуск (например,
локально, или отдельным прогоном без полного деплоя) остаётся доступен через workflow «Run BFF Migrations
(dev, manual)» или напрямую:

```bash
# скопировать deploy/migration/.env.example -> deploy/migration/.env и заполнить
docker compose -f deploy/migration/docker-compose.migrations.yml --profile migrations up --abort-on-container-exit
```

Учётная запись `DB_MIGRATION_USER` (`bff_admin`) — владелец схемы `bff`: создаёт и меняет таблицы внутри
неё, но саму схему, базу и роли (`bff_admin`/`bff_user`) заводит инфраструктура заранее, не миграции.
Это архитектурный факт, а не настройка стенда, поэтому `DB_SKIP_SCHEMA_CREATE: "true"` зашит прямо в
[`docker-compose.migrations.yml`](deploy/migration/docker-compose.migrations.yml) (переопределяет то, что
может лежать в `.env` — `environment:` в compose всегда сильнее `env_file:`). Рабочая учётка приложения
(`DB_USER`/`DB_PASSWORD` в `deploy/.env`, `bff_user`) прав на DDL не имеет вообще. `DB_MIGRATION_PASSWORD`
приходит из Vault, тем же механизмом — см. раздел «Vault» выше.

Если всё же нужно, чтобы `entrypoint.sh` сам создавал схему (`CREATE SCHEMA IF NOT EXISTS`) — например,
для локальной разработки без готовой заранее схемы — придётся поменять `"true"` на `"false"` прямо в
`docker-compose.migrations.yml` (значение в `.env` тут не поможет: `environment:` в compose всегда
сильнее `env_file:`), и учесть, что сама эта команда требует `CREATE` на базу для одной попытки её
выполнить, независимо от `IF NOT EXISTS` (проверка прав идёт раньше проверки «уже существует или нет»).

## Начальные данные

`scripts/001_seed_initial_data.sql` — единый файл начальных данных: создаёт системную группу `admins`,
регистрирует все коды ресурсов, которые знает текущая версия кода (базовые `users`/`groups`/`permissions`
и доменные `objects`/`sensors`/`predictions`/`tasks`/`incidents`/`schedule`/`assigned_objects`/
`engineers`/`presence`/`model_settings`), выдаёт группе полный доступ (маска 127) на каждый из них, и
на действительно новом стенде (в `admins` совсем никого нет) — заводит плейсхолдер-профиль первого
администратора и сразу добавляет его в `admins`.

**Выполняется автоматически** — `deploy/migration/entrypoint.sh` прогоняет этот файл сразу после
`dotnet ef database update`, на каждом деплое (и вручную, и через `deploy-dev.yml`). Идемпотентен —
безопасно гонять повторно (плейсхолдер администратора не создаётся заново, если в группе уже есть хоть
один участник — см. комментарий у шага 6 в самом файле). Обычный SQL, без psql-специфичных `\set`/`:var`
— схема захардкожена внутри как `bff` (поменяй, если у тебя `DB_SCHEMA` называется иначе).

После первого прогона на новом стенде — обязательно поменять заглушку `auth_user_id`
(`__bootstrap_admin__`) у созданного профиля на настоящий `sub` пользователя из `auth` (саму строку не
удалять, просто `UPDATE`, команда есть закомментированной в конце файла) — иначе войти под этим профилем
не получится, `TokenAuthenticationMiddleware` сопоставляет пользователя по `auth_user_id`.

Прогнать вручную, отдельно от миграций (например, после того как в файл добавили новый ресурс) —
`scripts/seed-via-vault.sh`: сам логинится в Vault (по `VAULT_TOKEN` или по AppRole через
`VAULT_ROLE_ID`/`VAULT_SECRET_ID`), достаёт пароль `bff_user` из `secret/tf/postgres/bff` и прогоняет
`001_seed_initial_data.sql`:

```bash
VAULT_ADDR=http://vault:8200 VAULT_TOKEN=<ваш_токен> DB_HOST=<хост_postgres> \
  ./scripts/seed-via-vault.sh
```

Нужны в PATH `curl`, `jq`, `psql`. Полный список переменных и пример запуска от AppRole — в шапке самого
файла. Без этого скрипта — то же самое руками: достать пароль из
`secret/tf/postgres/bff` (ключ `TF_PG_BFF_USER_PASSWORD`, значение в base64) и

```bash
PGPASSWORD='<пароль>' psql -h <хост> -U bff_user -d tf -f scripts/001_seed_initial_data.sql
```
