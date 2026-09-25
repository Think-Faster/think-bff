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

## Docker

Запуск сервиса (после того как появится сеть `app-network` и переменные в `deploy/.env`, скопированном
из `deploy/.env.example`):

```bash
docker compose -f deploy/docker-compose.yml up -d --build
```

Порты наружу не публикуются — сервис доступен только другим контейнерам в `app-network` (nginx, вероятно
позже — think-front). Контейнер БД в этом compose не описан: база разворачивается отдельно.

## Миграции

**Не выполнялось при разработке.** Файлы миграции уже сгенерированы (`dotnet ef migrations add`,
единственное разрешённое агенту действие сверх создания файлов — раздел 1 ТЗ) и лежат в
`src/BFF.Context/Migrations`. Применение — отдельный, ручной, одноразовый шаг, когда БД будет готова:

```bash
# скопировать deploy/migration/.env.example -> deploy/migration/.env и заполнить
docker compose -f deploy/migration/docker-compose.migrations.yml --profile migrations up --abort-on-container-exit
```

Контейнер миграций сначала создаёт схему (`CREATE SCHEMA IF NOT EXISTS`), затем применяет
`dotnet ef database update`. Учётная запись `DB_MIGRATION_USER`/`DB_MIGRATION_PASSWORD` должна иметь
право `CREATE` на базу; рабочая учётка приложения (`DB_USER`/`DB_PASSWORD` в `deploy/.env`) такого права
иметь не должна.

Если схему создаёт инфраструктура заранее, а `DB_MIGRATION_USER` намеренно без `CREATE` на базу — поставь
`DB_SKIP_SCHEMA_CREATE=true` в `deploy/migration/.env`: `CREATE SCHEMA IF NOT EXISTS` всё равно требует
`CREATE` на базу для самой попытки выполнить команду (проверка прав идёт раньше проверки «уже существует
или нет»), так что без этого флага шаг упадёт `permission denied`, даже если схема уже на месте.

## Начальные данные

`scripts/001_seed_initial_data.sql` — единый файл начальных данных: создаёт системную группу `admins`,
регистрирует все коды ресурсов, которые знает текущая версия кода (базовые `users`/`groups`/`permissions`
и доменные `objects`/`sensors`/`predictions`/`tasks`/`incidents`/`schedule`/`assigned_objects`/
`engineers`/`presence`/`model_settings`), и выдаёт группе полный доступ (маска 127) на каждый из них.
**Файл создан, но не выполнялся.** Идемпотентен — безопасно гонять повторно (при переезде на новое
окружение, или после добавления в файл кода нового ресурса). Обычный SQL, без psql-специфичных `\set`/
`:var` — схема захардкожена внутри как `bff` (поменяй, если у тебя `DB_SCHEMA` называется иначе), так
скрипт одинаково работает и через настоящий `psql`, и через любой GUI-клиент/раннер. Выполняется вручную,
после применения миграций:

```bash
psql -f scripts/001_seed_initial_data.sql
```

Первого администратора нужно добавить в группу `admins` вручную — команда для этого есть закомментированной
в конце самого скрипта.
