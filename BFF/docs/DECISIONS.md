# Решения по неоднозначностям

Ведётся по ходу разработки, согласно правилу 6 раздела 1 технического задания: при неоднозначности
агент принимает решение сам, фиксирует его здесь и продолжает работу, не останавливаясь на уточнениях.

## Тело ошибок — camelCase, как и всё остальное

Middleware (`ExceptionHandlingMiddleware`, `TokenAuthenticationMiddleware`,
`PermissionDeniedResultHandler`) пишут JSON-тело ошибки напрямую через `JsonSerializer.Serialize`, минуя
MVC-пайплайн — а значит, и его конфиг сериализации (camelCase по умолчанию в ASP.NET Core). Без явной
настройки они бы отдавали `{"Code": ..., "Message": ...}` (PascalCase — как в C#-классе `ErrorResponse`),
пока все успешные ответы контроллеров — `{"code": ..., "lastName": ...}` (camelCase). Заведён общий
`BFF.WebApi.Extensions.JsonDefaults.CamelCase` (заодно и `DictionaryKeyPolicy = CamelCase` — иначе поля
внутри `details` у ошибок валидации остались бы `LastName` вместо `lastName`), используется во всех трёх
местах.

## `DB_SKIP_SCHEMA_CREATE` в контуре миграций

Раздел 5, шаг 4 ТЗ поручает `entrypoint.sh` создавать схему (`CREATE SCHEMA IF NOT EXISTS`) перед
`dotnet ef database update`, предполагая, что `DB_MIGRATION_USER` имеет `CREATE` на базу. На практике
инфраструктура может создавать схемы заранее сама, а миграционной учётке сознательно не давать `CREATE`
на базу (только права внутри уже существующей схемы) — и тогда `CREATE SCHEMA IF NOT EXISTS` всё равно
падает `permission denied`, потому что в Postgres проверка прав на выполнение самой команды идёт раньше
проверки «уже существует или нет»; `IF NOT EXISTS` в этом случае не спасает. Добавлена переменная
`DB_SKIP_SCHEMA_CREATE` (по умолчанию `false`) — при `true` `entrypoint.sh` пропускает шаг создания схемы
и сразу переходит к `dotnet ef database update`. См. `deploy/migration/entrypoint.sh` и
`deploy/migration/.env.example`.

## `JwtSecurityTokenHandler` переименовывает `sub` — отключили маппинг

`JwtSecurityTokenHandler.ValidateToken` по умолчанию прогоняет claim'ы через
`DefaultInboundClaimTypeMap` и переименовывает известные короткие имена (`sub`, `name`, `email` и т.д.)
в legacy XML/SOAP URI — `sub` конкретно превращается в
`http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`. Из-за этого
`principal.FindFirst("sub")` (через `AUTH_USER_ID_CLAIM`) не находил ничего, хотя в самом JWT `sub`
присутствовал — токен от `tf-auth` даже дублирует значение под обоими именами, что и натолкнуло на
причину. Фикс — `new JwtSecurityTokenHandler { MapInboundClaims = false }` в `JwtValidator.ValidateAsync`:
имена claim'ов в `ClaimsPrincipal` теперь совпадают с тем, что реально в payload, без скрытого
переименования.

## `AUTH_JWKS_URL` отдаёт PEM-ключ, а не JWKS-документ

По факту (проверено запросом к реальному `tf-auth`): эндпоинт, на который указывает `AUTH_JWKS_URL`,
возвращает не JSON-документ вида `{"keys": [...]}` (стандартный JWKS), а обычный текст —
PEM-encoded публичный ключ (`-----BEGIN PUBLIC KEY-----...`). `Microsoft.IdentityModel.Tokens.JsonWebKeySet`
ожидает JSON и падает с `IDX10805` на любом другом теле ответа.

`JwtValidator.ParseSigningKeys` теперь понимает оба формата: если тело начинается с `{` — парсит как
JWKS; иначе — пытается импортировать как PEM (`RSA.ImportFromPem`, при неудаче — `ECDsa.ImportFromPem`).
Так, если auth-сервис в будущем всё же перейдёт на настоящий JWKS, менять код BFF не придётся.

Отдельно: ошибки получения/парсинга ключа (сеть недоступна, таймаут, нераспознанный формат) теперь не
проваливаются в общий `ArgumentException` → `400 bad_request` (как было раньше — вводило в заблуждение,
будто виноват вызывающий), а оборачиваются в `JwksUnavailableException` и репортятся как `503
auth_service_unavailable`: без рабочего ключа нельзя провалидировать вообще никакой токен, это не вина
клиента запроса. См. `JwksUnavailableException` и обработку в `TokenAuthenticationMiddleware`.

## Маршруты контроллеров без префикса `api/`

По факту устройства nginx во внешней инфраструктуре (файл `nginx.conf`, не в этом репозитории):
`location /api/bff/ { proxy_pass http://tf-bff:8080/; }`. При совпадающих завершающих `/` у `location` и
у `proxy_pass` nginx **вырезает** совпавший префикс `/api/bff/` и проксирует остаток пути как есть —
`GET /api/bff/users` долетает до контейнера как `GET /users`, без какого-либо `/api`. Поэтому
`UsersController`/`GroupsController`/`PermissionsController` больше не объявляют `[Route("api/...")]` —
только `users`, `groups` и голые экшен-маршруты (`permissions/me`, `resources` и т.д., без контроллерного
префикса). `HealthController` этого изменения не потребовал: он с самого начала был на `/health/...`, без
`/api`, что уже совпадало с тем, что реально приходит от nginx. Подробности путей — `docs/API.md`.

## Без сырого SQL в коде приложения

По прямому указанию в ходе разработки: весь доступ к данным в C#-коде идёт через обычные методы EF Core
(LINQ, `ExecuteDeleteAsync`, `AddRangeAsync`, `SaveChangesAsync`), без строк с SQL в репозиториях и
сервисах. Это отменяет три места, где ТЗ в разделах 6.3 и 7.2–7.3 приводит SQL как референс:

- **Advisory-lock графа групп (раздел 6.3).** `pg_advisory_xact_lock` — постгресовая примитива без
  представления в EF; заменена на обычный трекаемый апдейт единственной строки `rbac_version` через
  `DbContext.SaveChangesAsync`. Postgres держит блокировку строки до конца транзакции точно так же, как
  держал бы advisory lock, а сам апдейт — это уже обязательный по разделу 7.4 инкремент версии кэша, так
  что один вызов `IGroupGraphRepository.LockGraphAsync` закрывает обе задачи. См.
  [`GroupGraphRepository.LockGraphAsync`](../src/BFF.Context/Repositories/GroupGraphRepository.cs).
- **Пересчёт `group_closure` (раздел 6.3).** Вместо рекурсивного CTE — рёбра (`group_members` с
  `member_type = group`) вычитываются через LINQ, транзитивное замыкание считается в памяти обходом в
  ширину (мирроря ограничение `depth < 32` из референсного запроса), а таблица перезаписывается через
  `ExecuteDeleteAsync` + `AddRangeAsync` + `SaveChangesAsync`. См.
  [`GroupGraphRepository.RecomputeClosureAsync`](../src/BFF.Context/Repositories/GroupGraphRepository.cs).
- **Эффективные права пользователя (разделы 7.2–7.3).** Группы пользователя — через LINQ-джойн
  `group_members`/`group_closure`; маски грантов агрегируются побитовым `|` в C# (`Aggregate`) вместо
  `bit_or` в SQL. Точечная проверка `HasPermissionAsync` переиспользует тот же агрегированный результат.
  См. [`PermissionRepository`](../src/BFF.Context/Repositories/PermissionRepository.cs).

Единственное место, где строка SQL остаётся — `HasCheckConstraint(name, sql)` в конфигурациях EF
(например, `member_type IN (1,2)`). Это официальный fluent-API EF Core для CHECK-ограничений: у него нет
LINQ-эквивалента, а сама конструкция — DDL для миграции, а не запрос в бизнес-логике.

## Дополнительные типы сверх карты файлов раздела 3

ТЗ описывает структуру решения, но не исчерпывающий список типов. Помимо перечисленного, потребовались:

- `Entities/RbacVersion.cs` и `Configurations/RbacVersionConfiguration.cs` — таблица `rbac_version` явно
  требуется разделом 7.4 и чек-листом (раздел 15), но отсутствует в дереве раздела 3.
- `Constants/ResourceCodes.cs` (`BFF.Models`) — константы кодов `users`/`groups`/`permissions`, на
  которые ссылается раздел 9.
- `Enums/PermissionFlagsExtensions.cs`, `PrincipalTypeExtensions.cs`, `MemberTypeExtensions.cs`
  (`BFF.Models`) — конвертация между битовой маской/enum'ами и строковыми представлениями API
  (`"read"`, `"user"`, `"group"` и т.д.), нужна в нескольких слоях и логичнее всего живёт рядом с enum'ами,
  у которых нулевые зависимости.
- `Exceptions/ConflictException.cs` (`BFF.Application`) — общий 409 для дублирующегося кода
  группы/ресурса и для попытки удалить системную группу; `CycleDetectedException` в ТЗ покрывает только
  цикл в графе групп.
- `Services/IHealthService.cs` / `HealthService.cs` (`BFF.Application`) — по диаграмме зависимостей
  раздела 3 `BFF.WebApi` не ссылается на `BFF.Context`, поэтому `HealthController` не может обратиться к
  `BffDbContext.Database.CanConnectAsync` напрямую; обёрнуто сервисом в Application.
- `Contracts/Common/AuthenticatedUserDto.cs` — минимальная проекция пользователя, которая нужна
  `TokenAuthenticationMiddleware` (в `BFF.WebApi`) для резолва `sub` из токена в пользователя BFF, снова
  из-за отсутствия прямой ссылки `WebApi -> Context`.
- `ApplicationServiceCollectionExtensions.cs` (`BFF.Application`) — регистрация `DbContext` и
  репозиториев (`BFF.Context`) вынесена в Application ровно по той же причине: композиционный корень в
  `Program.cs` не должен и не может ссылаться на `BFF.Context` напрямую.
- `WebApi/Authorization/PermissionPolicyProvider.cs` — `IAuthorizationPolicyProvider`, без которого
  динамические политики `perm:{resource}:{mask}` из раздела 9 не могут быть построены рантаймом ASP.NET
  Core.
- `WebApi/Authorization/PermissionDeniedResultHandler.cs` — см. ниже.
- `WebApi/Extensions/HttpContextExtensions.cs` содержит и сам `CurrentUser` record (в ТЗ упомянут только
  как обращение `HttpContextExtensions.GetCurrentUser()`, без явного файла для типа).

## Аутентификация без `AddAuthentication()`

`TokenAuthenticationMiddleware` — единственное место, разбирающее токен (раздел 8), и он завершает
конвейер ответом 401 сам, не вызывая `next()`, если токена нет или он невалиден. К моменту, когда запрос
доходит до стандартного `UseAuthorization()` ASP.NET Core, `HttpContext.User` уже гарантированно
установлен серединным ПО. Поэтому:

- `AddAuthentication()` / `UseAuthentication()` не регистрируются вовсе — они не нужны без своей схемы.
- Вместо дефолтного поведения `Forbid`/`Challenge` (которому нужен `IAuthenticationService`, то есть
  зарегистрированная схема) реализован свой `IAuthorizationMiddlewareResultHandler`
  (`PermissionDeniedResultHandler`), который сразу пишет тело `{ code: "permission_denied", ... }` или
  `{ code: "unauthenticated", ... }` в ответ.

## `GET /api/groups/{id}`: три LINQ-запроса вместо одного с двумя LEFT JOIN

`group_members.member_id` — полиморфная ссылка без FK (раздел 4.1), поэтому EF не может построить один
JOIN сразу на `users` и `groups` по типу строки. `GroupService.GetDirectMembersAsync` делает три
точечных индексных запроса (рёбра группы → пользователи по списку id → вложенные группы по списку id) и
собирает `GroupMemberDto` в памяти — вместо одного SQL с двумя `LEFT JOIN`, как буквально написано в
разделе 10.2.

## Батч добавления участников группы: проверка цикла по всей пачке

`POST /api/groups/{id}/members/batch` требует одного пересчёта замыкания на всю пачку (раздел 10.2), но
`group_closure` не отражает рёбра, ещё не сохранённые в этой же пачке. Поэтому цикл проверяется не через
`group_closure`, а через adjacency-граф в памяти: существующие рёбра `group_members` плюс те рёбра из
текущей пачки, что уже прошли проверку — иначе два ребра, поданных в одном вызове и замыкающие цикл
только вместе (`A→B` и `B→A`), проскочили бы мимо проверки.

## Идемпотентность повторного добавления участника группы

Для `POST /api/users/{id}/groups` ТЗ прямо требует пропускать существующее членство молча (раздел 10.1).
Для `POST /api/groups/{id}/members` и его batch-варианта явного указания нет — решено вести себя так же
(тихий пропуск дубликата), а не возвращать 409, ради единообразия UX двух почти одинаковых операций.

## `PUT /api/groups/{id}` и защита кода системной группы

Раздел 10.2 одновременно говорит, что тело `PUT /api/groups/{id}` — это только `{ name }`, и что
изменение `code` системных групп запрещено с 409 `system_group_protected`. Поскольку в контракте нет
поля `code`, этот путь недостижим через API как он специфицирован — код ошибки `system_group_protected`
всё равно определён и используется, но для другого места: `DELETE /api/groups/{id}` системной группы.

## `GET /api/resources`: только аутентификация, без отдельного права

Раздел 10.3 не называет требуемое право для чтения справочника ресурсов (в отличие от `POST
/api/resources`, для которого явно указано `permissions:manage`). Решено: `GET` — это справочные данные,
достаточно быть аут, `POST` — `RequirePermission(permissions, Manage)`.

## Идентификаторы

Раздел 2 допускает UUID v7 «если доступна библиотека». Отдельная библиотека не добавлялась (нет сетевого
доступа при написании кода и незачем тянуть лишнюю зависимость), поэтому везде используется
`Guid.NewGuid()`, как и разрешает ТЗ по умолчанию.

## `LOG_LEVEL` → Serilog

Serilog штатно конфигурируется через `Serilog:MinimumLevel:Default` в `appsettings`, а ТЗ (раздел 11)
описывает плоскую переменную `LOG_LEVEL`. `Program.cs` читает `LOG_LEVEL` явно и передаёт как
`MinimumLevel.Is(...)`, оставляя `ReadFrom.Configuration` для остальных настроек Serilog при желании
расширить конфиг позже.

## Что фактически выполнялось

Помимо создания файлов: `dotnet restore` и `dotnet build` (раздел 1, пункт 5 — допустимая проверка
компилируемости, прогонялась несколько раз по ходу правок) и `dotnet ef migrations add InitialRbac`
(раздел 1, пункт 2 — единственное разрешённое действие сверх создания файлов). Миграция генерировалась
дважды: первый прогон вскрыл настоящий баг в DI (`IPermissionCache` был зарегистрирован как singleton,
но зависит от scoped `IPermissionRepository` — упало бы при первом же старте приложения), поэтому после
исправления (`AddScoped` вместо `AddSingleton`, см. `ApplicationServiceCollectionExtensions`) между двумя
`add` был вызван `dotnet ef migrations remove`, чтобы не оставлять в репозитории миграцию, сгенерированную
до фикса. `migrations remove` по своей логике пытается проверить, не применена ли миграция к реальной БД;
попытка подключиться к `localhost:5432` (тем же фиктивным connection string, что и в
`BffDbContextFactory`) завершилась ошибкой аутентификации — то есть ни чтения, ни записи в какую-либо БД
не произошло, `dotnet ef` лишь предупредил об этом и продолжил без БД. `dotnet ef database update`,
`docker compose up`, `psql` и любые другие обращения к базе не выполнялись.

## Целевой SDK

На машине установлен .NET SDK 9.0.311, при этом рантайм 8.0.24 тоже присутствует. Все проекты явно
таргетятся на `net8.0` через `Directory.Build.props`, как требует раздел 2 (LTS) — SDK 9 умеет собирать
`net8.0`-проекты без изменений в TFM.
