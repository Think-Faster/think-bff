# Как добавить новую сущность (на примере «Заявок»)

Инструкция для разработчика, который будет добавлять в BFF новую доменную сущность с CRUD-ручками —
например, «Заявки» (`requests`). Управление правами на новую сущность отдельно писать не нужно:
`[RequirePermission]` и весь механизм авторизации (раздел 9 ТЗ, `docs/DECISIONS.md`) уже универсален для
любого кода ресурса — новая сущность получает его «бесплатно», если пройти шаги ниже.

Код здесь не приводится намеренно — это описание шагов и того, по какому существующему файлу их
копировать. Везде, где сказано «по аналогии с Users» — открой `UsersController.cs`/`UserService.cs`/
`UserDto.cs` и так далее, скопируй структуру, замени имена.

Слои и порядок — снизу вверх, в точности как диаграмма зависимостей в `README.md`:
`BFF.Models → BFF.Context → BFF.Contracts → BFF.Application → BFF.WebApi`. Двигайся в этом порядке —
каждый следующий слой ссылается на предыдущий, не наоборот.

## 0. Спланируй сущность до кода

- Какие поля у «Заявки» и какие из них `NOT NULL`.
- Нужны ли все семь прав (`create/read/update/delete/export/import/manage`) или подмножество — для
  большинства сущностей достаточно `create/read/update/delete`, остальные заводи только когда реально
  нужны (`export`/`import` — для выгрузки/загрузки, `manage` — как надправо остаётся всегда осмысленным).
- Код ресурса (то, что уйдёт в таблицу `resources.code` и в `[RequirePermission]`) — короткий, снейк/кебаб
  без пробелов, например `requests`. Дальше в инструкции используется именно он.
- Есть ли связь с пользователем/группой (например «кто создал заявку», «на кого назначена») — если да,
  храни как обычный `Guid`-столбец (`created_by_user_id` и т.п.) **без** EF-навигации/FK на `users`,
  если это не критично для целостности. Модель уже сознательно избегает лишних кросс-модульных FK там,
  где полиморфизм или независимость модулей важнее (см. `group_members` в `docs/DECISIONS.md`) — не
  плоди связи ради связей.

## 1. `BFF.Models` — сущность

Файл: `src/BFF.Models/Entities/Request.cs`.

- Обычный POCO: `Guid Id`, поля сущности, `CreatedAt`/`UpdatedAt` как `DateTimeOffset` (весь проект хранит
  время в `timestamptz`, UTC — см. раздел 2 ТЗ и уже существующие сущности `User`/`Group` как образец).
- Никаких атрибутов EF на самой сущности — вся конфигурация (имена колонок, ключи, индексы) отдельно, в
  `BFF.Context` (это правило проекта: «шесть таблиц RBAC описаны конфигурациями, а не атрибутами» из
  чек-листа ТЗ — держи его и для новых сущностей).
- Если у сущности свой набор допустимых значений (статус заявки и т.п.) — заведи enum в
  `src/BFF.Models/Enums/`, по аналогии с `PermissionFlags`/`MemberType`.
- Опционально, но рекомендуется: добавь константу кода ресурса в
  `src/BFF.Models/Constants/ResourceCodes.cs` (`public const string Requests = "requests";`) — так
  `[RequirePermission(ResourceCodes.Requests, ...)]` в контроллере не полагается на строковый литерал,
  который легко опечатать.

`BFF.Models` ни от чего не зависит — не тяни сюда ничего из `BFF.Context`/`BFF.Contracts`.

## 2. `BFF.Context` — EF-конфигурация, `DbSet`, миграция

Файл: `src/BFF.Context/Configurations/RequestConfiguration.cs`, реализует `IEntityTypeConfiguration<Request>`
— скопируй структуру `UserConfiguration.cs` или `GroupConfiguration.cs`:

- `builder.ToTable("requests")` — **без схемы**. Это критично: во всём проекте схема не зашивается в
  код/миграцию нигде (`HasDefaultSchema`, `ToTable(name, schema)` — под запретом), она приходит только
  через `Search Path` в строке подключения (раздел 5 ТЗ, `docs/DECISIONS.md`). Если добавишь схему тут —
  сломаешь переносимость между `bff_dev`/`bff_stage`/`bff_prod`.
- `builder.HasKey(...)`, `Property(...).HasColumnName("snake_case_name")` — на каждое поле явно, имена
  колонок snake_case (проектное соглашение, раздел 2 ТЗ).
- Индексы/уникальные ограничения — через `builder.HasIndex(...)`, чек-констрейнты — через
  `builder.ToTable("requests", tb => tb.HasCheckConstraint(...))`, если нужны (это единственное место,
  где в проекте по-прежнему остаётся сырой SQL-фрагмент — сам API EF Core для CHECK-ограничений так
  устроен, см. `docs/DECISIONS.md`, раздел «Без сырого SQL»).

В `src/BFF.Context/BffDbContext.cs`:
- добавь `public DbSet<Request> Requests => Set<Request>();`;
- зарегистрируй конфигурацию в `OnModelCreating`: `modelBuilder.ApplyConfiguration(new RequestConfiguration());`.

### Миграция

Та же команда, что уже использовалась для `InitialRbac` (раздел 14 ТЗ):

```bash
cd BFF
dotnet ef migrations add AddRequests --project src/BFF.Context --startup-project src/BFF.WebApi --output-dir Migrations
```

Требует `dotnet-ef` (`dotnet tool install --global dotnet-ef --version 8.*`, если ещё не стоит).

Что проверить в сгенерированном файле, как и раньше:
- имени схемы нигде нет (`grep -i schema` по файлу миграции — пусто);
- есть все нужные индексы/констрейнты, которые ты объявил в конфигурации.

**Миграцию сгенерировать — да, применить (`dotnet ef database update`) — нет.** Применение — тот же
ручной контур `deploy/migration`, что уже есть в репозитории; для него ничего не нужно дорабатывать, он
универсален для любой очередной миграции (запускается так же, как описано в `README.md`).

## 3. `BFF.Contracts` — DTO

Папка `src/BFF.Contracts/Requests/`, по аналогии с `Users`/`Groups`:

- `RequestDto` — полный объект для одиночного `GET`;
- `RequestListItemDto` — облегчённая версия для списка (если набор полей отличается; если нет — не плоди
  два одинаковых класса, можно использовать один и тот же DTO, как сделано для `ResourceDto`);
- `CreateRequestRequest`, `UpdateRequestRequest` — тела запросов;
- список — оборачивается в `PagedResult<RequestListItemDto>` из `Contracts/Common` (уже готов, ничего
  своего под пагинацию писать не нужно).

Свойства — обычные C#-`init`-проперти, PascalCase. В JSON они автоматически уйдут camelCase — это дефолт
ASP.NET Core для контроллеров, ничего специально настраивать не нужно (см. `docs/DECISIONS.md` про
`JsonDefaults.CamelCase` — та настройка касается только тела ошибок, которые middleware пишет в обход
MVC-пайплайна; успешные ответы контроллеров и так camelCase).

## 4. `BFF.Application` — сервис и валидация

### Сервис

`src/BFF.Application/Services/IRequestService.cs` + `RequestService.cs`, по образцу `IUserService`/
`UserService`. Обычно нужны:

- `ListAsync(search, page, pageSize, ct)` → `PagedResult<RequestListItemDto>`;
- `GetAsync(id, ct)` → `RequestDto`, кидает `NotFoundException` если не найдено;
- `CreateAsync(request, ct)` → `RequestDto`;
- `UpdateAsync(id, request, ct)` → `RequestDto`;
- `DeleteAsync(id, ct)` — реши сразу, soft или hard (как у пользователей — параметр `?soft=`, или просто
  hard-delete, если для заявок нет смысла в мягком удалении).

`RequestService` инжектит `BffDbContext` напрямую (как `UserService`/`GroupService`) и работает через
обычный EF LINQ — без сырых SQL-запросов в коде (это правило действует для всего проекта, не только для
RBAC-таблиц, см. `docs/DECISIONS.md`).

**`rbac_version` трогать не нужно.** Он инкрементируется только при изменении RBAC-таблиц
(`users`/`groups`/`group_members`/`access_grants`) — «Заявки» в их число не входят, кэш прав их не
касается.

### Валидация

`src/BFF.Application/Validators/CreateRequestRequestValidator.cs` и `UpdateRequestRequestValidator.cs`
через `FluentValidation`, по образцу `CreateUserRequestValidator.cs`.

### Регистрация в DI

В `src/BFF.Application/ApplicationServiceCollectionExtensions.cs` добавь строку
`services.AddScoped<IRequestService, RequestService>();` рядом с уже существующими сервисами. Валидаторы
отдельно регистрировать не нужно — `AddValidatorsFromAssemblyContaining` уже сканирует всю сборку
`BFF.Application` и подхватит новые классы валидаторов автоматически.

## 5. `BFF.WebApi` — контроллер

`src/BFF.WebApi/Controllers/RequestsController.cs`, по образцу `UsersController.cs`/`GroupsController.cs`:

- `[Route("requests")]` — **без префикса `api/`**. nginx на инфраструктуре сам срезает `/api/bff/` перед
  проксированием на сервис, так что внутренний маршрут контроллера — голый (см. `docs/DECISIONS.md`,
  раздел «Маршруты контроллеров без префикса api/», и `docs/API.md`).
- На каждый экшен — `[RequirePermission(ResourceCodes.Requests, PermissionFlags.Read | Create | Update | Delete)]`
  в зависимости от операции — один в один как у `UsersController`/`GroupsController`. Это единственное
  место, где вообще упоминается право — дальше всё отрабатывает существующий механизм (следующий раздел).
- Валидация тела: инжектни `IValidator<CreateRequestRequest>`/`IValidator<UpdateRequestRequest>` в
  конструктор, вызови `.ValidateAndThrowAsync(request, ct)` перед вызовом сервиса — `ValidationException`
  уже перехватывается `ExceptionHandlingMiddleware` и превращается в `400 validation_failed`.
- Контроллер **не должен** видеть `BffDbContext` — только `IRequestService`. Это не только стиль:
  `BFF.WebApi` в принципе не ссылается на `BFF.Context` в этом проекте (см. диаграмму зависимостей в
  `README.md`), так что прямое обращение к контексту оттуда попросту не скомпилируется.

## 6. Зарегистрировать ресурс в таблице `resources`

Это шаг, без которого `[RequirePermission(ResourceCodes.Requests, ...)]` никогда не пропустит ни одного
запроса — если кода `requests` нет в таблице `resources`, у любого принципала просто не может быть на
него гранта, и любой запрос будет получать `403 permission_denied` от кого угодно, включая группу с
`Manage`-маской на всё **остальное**.

Заведи SQL-скрипт `scripts/002_seed_requests_resource.sql`, по образцу
`scripts/001_seed_admins_group.sql`: шапка-подпись (автор/дата/назначение/«не выполнялось при
разработке»), `\set ON_ERROR_STOP on`, `SET search_path TO :schema;`, дальше в одной транзакции —

- `INSERT INTO resources (code, name) VALUES ('requests', 'Заявки') ON CONFLICT (code) DO NOTHING;`
- если сразу нужно дать какой-то группе (например `admins`) права на новый ресурс — ещё один
  `INSERT INTO access_grants (...) SELECT ... ON CONFLICT (...) DO UPDATE SET permission_mask = ...;`,
  как шаг 3 в `001_seed_admins_group.sql`;
- финальный `INSERT ... rbac_version ... ON CONFLICT DO UPDATE SET value = rbac_version.value + 1;` —
  сбросить кэш прав, чтобы новые гранты подхватились сразу, а не ждали своего TTL.

Как и все SQL-скрипты в проекте — **создать, не выполнять**. Применяется вручную, один раз, после
миграции — тем же способом, что и `001_seed_admins_group.sql` (`README.md`, раздел «Начальные данные»).

Альтернатива на будущее (не вместо скрипта, а для эксплуатации): после деплоя тот же результат можно
получить через `POST /resources` и `POST /permissions/grants` (права `permissions:manage`,
`docs/FRONTEND_INTEGRATION.md`/`docs/API.md`) — но начальную регистрация ресурса всё равно стоит держать
в SQL-скрипте как воспроизводимую часть инфраструктуры, а не только через API вручную один раз.

## 7. Управление правами — ничего писать не нужно

Это и есть весь смысл текущей архитектуры RBAC (раздел 9 ТЗ): `PermissionPolicyProvider`,
`PermissionAuthorizationHandler`, `PermissionService`, `/permissions/grants`, `/permissions/check`,
`/permissions/me` — всё это уже работает с **любым** `resourceCode`, никакой доработки под конкретную
сущность не требуется. Как только ресурс `requests` появился в таблице `resources` (шаг 6) и на
контроллере расставлены `[RequirePermission]` (шаг 5):

- права на заявки выдаются/отзываются теми же `POST /permissions/grants`/`DELETE
  /permissions/grants/{id}`, что и для `users`/`groups` — ничего специфичного для «заявок» в этих
  эндпоинтах нет и не будет;
- `GET /permissions/me` для любого пользователя сам покажет `"requests": ["read", "create", ...]`, если
  права есть — фронтенду тоже ничего объяснять не нужно сверх того, что уже в
  `docs/FRONTEND_INTEGRATION.md`.

## 8. Обновить документацию

- `docs/API.md` — добавить раздел `Заявки — /requests` по образцу существующих.
- `docs/FRONTEND_INTEGRATION.md` — добавить TS-типы DTO и строки в таблицу эндпоинтов (раздел 5–6 того
  файла).
- `docs/DECISIONS.md` — только если по ходу пришлось принять неочевидное решение (soft vs hard delete,
  нестандартный набор прав и т.п.) — фиксируй так же, как остальные решения там.

## 9. Финальный чек-лист

- [ ] `dotnet build` всего солюшена — без ошибок.
- [ ] Миграция сгенерирована (`dotnet ef migrations add`), в её тексте нет имени схемы.
- [ ] Миграция и SQL-скрипт регистрации ресурса — **созданы, не выполнены**.
- [ ] Контроллер не обращается к `BffDbContext` напрямую — только через `IRequestService`.
- [ ] Каждый CRUD-эндпоинт закрыт `[RequirePermission(ResourceCodes.Requests, ...)]` с нужным битом.
- [ ] `rbac_version` в коде сервиса заявок не трогается (это не RBAC-таблица).
- [ ] Ресурс `requests` зарегистрирован в `resources` тем же SQL-скриптом, что и (опционально) стартовые
      гранты для `admins`.
- [ ] `docs/API.md` и `docs/FRONTEND_INTEGRATION.md` обновлены.
