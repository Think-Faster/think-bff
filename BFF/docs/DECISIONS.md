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

## Доменные сущности D1/D3/D4/D6-доп/D8 — реализация целиком за один проход

По итогам ревью документа аналитика «домены-и-сущности.md» (см. артефакт «Межсервисная модель данных
think-front») реализованы все ~20 сущностей из категории «точно в BFF»: топология (D1: `MonitoringObject`,
`Picket`, `Sensor`, `SensorLink`, `MapLayer`), прогнозы (D3: `Prediction` + `PredictionFactor`/
`PredictionEvidence`/`PredictionDecision`, `FactAlert`), заявки и работы (D4: `WorkTask` + `TaskPrediction`/
`TaskAssignment`/`TaskReport`/`TaskReturn`, `Incident`), дополнения D6 (`ScheduleEntry`, `UserActivity`,
`AssignedObject`, `Brigade`, `EngineerProfile`) и админ-настройки D8 (`ModelVersion` — только control-часть,
`Coefficient`, `RetrainJob`, `IgnoredRange`). Одна миграция `AddDomainEntities`.

Решения, принятые по ходу:

- **`MonitoringObject`, не `Object`; `WorkTask`, не `Task`.** Голые имена конфликтуют с `System.Object` и
  `System.Threading.Tasks.Task`. Таблицы в БД всё равно `objects`/`tasks`, как в исходном документе —
  расхождение только в имени C#-класса.
- **DTO и валидаторы сгруппированы по агрегату в один файл**, а не один класс — один файл, как для
  `Users`/`Groups`/`Permissions`. При ~20 сущностях и 3–4 DTO на каждую это ~90 файлов против 9 — тот же
  код, тот же уровень организации (namespace/подпапка на агрегат), просто без отдельного файла на каждый
  маленький POCO.
- **Enum'ы новых доменов сериализуются как есть, через `JsonStringEnumConverter` (camelCase) в
  `Program.cs`**, а не через ручные `ToApiString()`/`TryParse` расширения, как `PrincipalType`/
  `MemberType`. Тех было два и они уже написаны руками; тут — десяток, ручной конвертер на каждый не
  оправдан.
- **`sensor.state/last_value/last_ts` не заведены.** Как отмечено в разборе доменного документа, это
  снимок потока, а не карточка — источник правды здесь `tf-funnel`, не BFF. `Sensor` в этой реализации —
  только словарная часть.
- **`prediction_score`, полная почасовая сетка модели — не заведена.** По объёму (~11 тыс. строк/сутки)
  это ближе к аналитике, чем к CRUD-домену BFF — решение отложено до разговора с ML-командой (см. артефакт,
  раздел «пограничные случаи»).
- **`model_version` — только control-часть** (переключение `is_default`, кто/когда). Реестр
  версий/метрик/`registry_ref` остаётся за `tf-model`; в BFF `ModelVersion.Id`/`Name` — по сути кэш-ссылка,
  не полноценный реестр.
- **Межсервисных FK по-прежнему нет** (`WorkTask.ObjectId`, `Prediction.ObjectId` и т.п. — обычные `int`
  без `HasOne`/FK на `objects`), а вот **внутридоменные FK есть** (`PredictionFactor` → `Prediction`,
  `TaskAssignment` → `WorkTask` и т.д., все каскадные). Тот же принцип, что уже был у `group_members`:
  полиморфные/кросс-доменные связи — по id без FK, связи внутри одного агрегата — с FK.
- **Пользовательские ссылки (`DispatcherId`, `EngineerId`, `CreatedBy` и т.п.) — тоже без FK на `users`.**
  RBAC (D6-ядро) в этой модели — свой домен наравне с D1/D3/D4/D8, и правило «без FK между доменами»
  применено и здесь, не только к `object_id`.
- **Атомарный захват заявки** (`WorkTaskService.TakeAsync`, «кто первый взял — тот и ведёт», раздел 4.1
  исходного документа) — через `Where(...).ExecuteUpdateAsync(...)` с условием `Status == New` в самом
  `WHERE`: обычный EF LINQ bulk-update, не сырой SQL, семантически равнозначен `UPDATE ... WHERE status =
  'NEW'` из документа. Нулевое число затронутых строк → `409 task_already_taken`.
- **Присутствие (`UserActivity`) пишет `TokenAuthenticationMiddleware`** на каждый успешный запрос, но с
  троттлингом через `IMemoryCache` (не чаще раза в `PresenceOptions.FlushIntervalSeconds`, по умолчанию
  30 сек) — прямое требование раздела 6.4 исходного документа: «запись на каждый запрос бьёт по базе».
  Ошибка трекинга присутствия никогда не валит сам запрос (try/catch с логом).
- **Окно «онлайн» и интервал троттлинга — конфигурируемые** (`PresenceOptions`, секция `Presence` в
  конфиге), не захардкожены — документ прямо просит «вынести в настройку, а не зашивать в код».
- **Коэффициенты и решение диспетчера остаются в БД (`PredictionDecision`, `Coefficient` с версией), но
  топики `tf.dispatch.decisions`/`tf.dispatch.settings` не заведены** — это инфраструктурная работа вне
  кода BFF (см. артефакт, пункт 8 открытых вопросов); без них consumer на стороне `tf-model` работать не
  будет, но API и хранение на стороне BFF уже готовы.
- **Новые коды ресурсов регистрируются в том же `scripts/001_seed_initial_data.sql`**, что и базовые
  (создан, не выполнялся) — см. ниже отдельное решение про объединение seed-скриптов в один файл.

## Один seed-файл вместо нескольких пронумерованных

`001_seed_admins_group.sql` и `002_seed_domain_resources.sql` объединены в один
`scripts/001_seed_initial_data.sql`. Причина — по просьбе пользователя: при разворачивании нового
окружения не хочется помнить порядок и держать в голове несколько отдельных файлов, которые нужно
прогнать один за другим. Заодно шаг выдачи прав `admins` упрощён: было явное перечисление кодов ресурсов
в `WHERE r.code IN (...)`, стало `CROSS JOIN resources` без фильтра — `admins` получает `manage` на
**все** зарегистрированные ресурсы, включая те, что будут дописаны в этот же файл в будущем, без
необходимости трогать блок выдачи прав при каждом добавлении новой сущности.

Обсуждали и вариант «завести регистрацию `resources` через `HasData()` в миграции» (EF Core умеет
генерировать `InsertData` для статичных справочников) — отклонили: `resources.id` сейчас
`GENERATED ALWAYS AS IDENTITY`, а `HasData` требует явных `Id`, значит потребовалась бы ещё и смена на
`GENERATED BY DEFAULT AS IDENTITY`. А главное — выдачу прав `admins` в миграцию всё равно нельзя было бы
перенести (группа `admins` создаётся отдельным seed-шагом, не гарантированно уже применённым к моменту
миграции, плюс это осознанное решение о доступе, а не структура схемы) — единый SQL-файл решает
исходную проблему («не гонять несколько скриптов по отдельности») без смешивания схемы и данных доступа.

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

## Переход на Vault — только `postgres/bff`, без Kafka/RabbitMQ/Redis/`app/tf-bff`

ТЗ по Vault (раздел 3) описывает общий набор путей — `postgres/bff`, `kafka/bff`, `rabbit/bff`, `redis`,
`app/tf-bff` — как шаблон для сервисов вообще, но прямо разрешает не заводить путь, если сервис его не
использует. В коде BFF на сегодня нет ни одного клиента Kafka/RabbitMQ/Redis (только EF Core + Npgsql) и
нет собственных секретов уровня приложения (SMTP, токены ботов, внешние API-ключи) — значит, единственный
реальный секрет — пароль Postgres, причём два разных для двух разных ролей (`bff_user` — приложение,
`bff_admin` — миграции). Заведён один путь `secret/tf/postgres/bff` с двумя ключами
(`TF_PG_BFF_USER_PASSWORD`, `TF_PG_BFF_ADMIN_PASSWORD`), остальные пути из шаблона не создавались.
Подробная инвентаризация и обоснование — `docs/VAULT_MIGRATION.md`.

## Vault: `vault-entrypoint.sh` берётся из ТЗ без изменений, оборачивает существующий `entrypoint.sh`

Скрипт `vault-entrypoint.sh` скопирован из приложения к ТЗ буквально (раздел 5.2 — «положить в корень
репозитория как есть»); никакая логика внутри не переписывалась. Для основного приложения он и раньше
не было отдельного entrypoint-скрипта — раньше `ENTRYPOINT` в `deploy/Dockerfile` был просто
`["dotnet", "BFF.WebApi.dll"]`, поэтому `vault-entrypoint.sh` подставлен как новый `ENTRYPOINT`, а старая
команда ушла в `CMD` (он передаёт её как `"$@"` в финальный `exec`). Для контура миграций уже существовал
свой `entrypoint.sh` (создание схемы + `dotnet ef database update`) — его переписывать под Vault было бы
избыточно и рискованно, поэтому `vault-entrypoint.sh` подставлен как `ENTRYPOINT`, а прежний
`/entrypoint.sh` — как `CMD`: одна и та же обёртка одинаково работает в обоих контейнерах, экспортирует
секреты в окружение и передаёт управление тому, что было раньше.

## Vault: `VAULT_ROLE_ID`/`VAULT_SECRET_ID` — через GitHub Environments, не repo-secrets

`docker-compose.yml`/`docker-compose.migrations.yml` требуют `VAULT_ROLE_ID`/`VAULT_SECRET_ID` через
`${VAR:?ошибка}` — то есть `docker compose` откажется стартовать без явной ошибки, если переменной нет в
окружении процесса, который его вызывает (а не только в `.env`-файле сервиса). Эти два значения относятся
к конкретному стенду (dev/prod у разных AppRole) и не должны лежать в `.env` на сервере вообще (раздел
5.2 ТЗ — принцип «Vault-креды не в файле, а в окружении процесса деплоя»). Поэтому они заведены как
секреты в GitHub Environments `dev`/`prod` (не repo-level secrets), обе задействующие эту схему джобы
(`deploy-dev.yml`, `run-bff-migrations.yml`) получили `environment: dev`, и значения прокидываются в
удалённую SSH-сессию через уже существующий паттерн `env:`+`envs:` у `appleboy/ssh-action` (тот же, что
раньше защитил `TARGET_BRANCH` от script-injection) — так `docker compose` на сервере видит их в своём
окружении и может подставить в `${VAULT_ROLE_ID}`/`${VAULT_SECRET_ID}` из compose-файла.

## Vault: переименования `DB_USER`/`DB_NAME`/`DB_MIGRATION_USER` под каноничные из ТЗ

Раздел 5.6 ТЗ фиксирует конкретные имена: пользователь приложения `bff_user`, пользователь миграций
`bff_admin`, база `tf`. Раньше в `.env.example`/`appsettings.json` были плейсхолдеры `bff_app`/
`bff_migrator`/`thinkfront` (введённые агентом на самом первом этапе, до этого ТЗ) — переименованы везде
(`deploy/.env.example`, `deploy/migration/.env.example`, `src/BFF.WebApi/appsettings.json`) для
консистентности с новым ТЗ и с тем, что реально будет создано в Vault/Postgres на стенде. Это только
переименование плейсхолдеров, не изменение поведения кода — сами переменные (`DB_USER`, `DB_NAME`,
`DB_MIGRATION_USER`) как читались, так и читаются.

## `seed-via-vault.sh` — ручной хелпер, не автосидинг при старте приложения

После перехода на Vault сидинг `001_seed_initial_data.sql` на новой инфраструктуре усложнился: пароль
БД больше не лежит в `.env`, его нужно сначала достать из Vault. Обсуждался вариант автоматически
создавать группу `admins` при старте сервиса, если её нет, — решили не делать так: это превратило бы
разовое, security-значимое действие (выдача полного доступа) в часть боевого пути приложения, требующую
аккуратной идемпотентности при нескольких репликах и добавляющую RBAC-риск в код, который просто должен
поднимать HTTP API. Вместо этого — `scripts/seed-via-vault.sh`: тонкая обёртка, повторяющая логин-логику
`vault-entrypoint.sh` (AppRole или готовый `VAULT_TOKEN`), которая один раз достаёт пароль `bff_user` из
`secret/tf/postgres/bff` и прогоняет уже существующий `001_seed_initial_data.sql` через `psql`. Ничего не
меняет в коде приложения, вызывается человеком вручную (как и сам SQL-файл раньше), просто снимает с него
шаг «вручную полезть в Vault за паролем».

## `DOCKER_NETWORK` без дефолта в обоих compose-файлах — из-за реального сбоя миграций на dev

Прогон workflow «Run BFF Migrations» на dev упал: контейнер `bff-migrations` 10 минут ретраил логин в
Vault и падал по таймауту SSH, хотя `tf-bff` с теми же `VAULT_ROLE_ID`/`VAULT_SECRET_ID` логинился
нормально. Диагноз (от второго разработчика/его агента, независимо перепроверен по `deploy/docker-
compose.yml` и `deploy/migration/docker-compose.migrations.yml` — в репозитории оба файла резолвили
сеть абсолютно одинаково, `${DOCKER_NETWORK:-app-network}`): вероятная причина — `DOCKER_NETWORK` в
`deploy/.env` и `deploy/migration/.env` **на сервере** не совпадали (или не был задан в одном из них),
и тихий дефолт на `app-network` цеплял контейнер миграций к сети, где `vault` не резолвится по имени
— но никакой ошибки при этом не происходило, только 60 неудачных попыток логина, каждая с 10-секундным
интервалом, прежде чем внешний SSH-таймаут обрывал всё. Убрали дефолт (`${DOCKER_NETWORK:?...}`, как уже
сделано для `VAULT_ROLE_ID` рядом) в обоих файлах: при пропущенном/несовпадающем значении `docker compose`
теперь падает мгновенно с понятной ошибкой вместо 10 минут молчаливых ретраев. Это не устраняет саму
причину (значения `DOCKER_NETWORK` в двух `.env`-файлах на сервере — вне репозитория, их выравнивание
остаётся ручным шагом), только переводит эту ошибку конфигурации из «непонятный долгий таймаут» в
«явная ошибка на старте контейнера».

## Миграции — теперь автоматически при деплое на dev, а не только вручную

По аналогии с тем, как уже сделано в сервисе `auth` (по слову заказчика — согласованная у него практика
для этой инфраструктуры): `deploy-dev.yml` теперь сам собирает и прогоняет контейнер миграций перед
пересборкой `tf-bff`, вместо того чтобы полагаться на отдельный ручной workflow `run-bff-migrations.yml`
после каждого мержа в `dev`. Добавлен `set -e` в начало SSH-скрипта деплоя (раньше его не было — ни одна
команда в скрипте не могла провалить весь шаг) и блок с тем же `docker compose --profile migrations
build/up --abort-on-container-exit/down`, что и в ручном workflow, — если миграция падает,
`--abort-on-container-exit` возвращает ненулевой код из `docker compose up`, `set -e` останавливает
скрипт, и `tf-bff` не собирается и не перезапускается на несовпадающей схеме. `run-bff-migrations.yml`
не удалён — оставлен как ручной инструмент (прогнать миграции отдельно от деплоя, например для отладки),
дублирование безопасно: `dotnet ef database update` идемпотентен для уже применённых миграций.

## Сеть консолидирована в одну `think-fast-net`, `DOCKER_NETWORK` убран

Инфраструктура пересобрала стенд на новой схеме (новая база, новые пароли) и объединила все docker-сети
в одну — `think-fast-net`; отдельной сети для Postgres (`postgree_app-network`) больше нет. Заодно
получили официальное подтверждение того, чего боялись раньше (см. запись «`DOCKER_NETWORK` без дефолта»
выше): именно рассинхрон значения `DOCKER_NETWORK` между `deploy/.env` и `deploy/migration/.env`
(указывал на сеть Postgres, а не на сеть Vault) не давал `bff-migrations` даже войти в Vault. Раз сеть
теперь одна и так и останется одной (инфра прямо это формулирует, без переменной в своём образце
compose), убрали `DOCKER_NETWORK` полностью и зашили `think-fast-net` буквально в оба compose-файла —
класс ошибки «два .env указывают на разные сети» теперь структурно невозможен, а не просто выявляется
быстрее.

## Хосты Postgres/Redis переименованы под новую топологию (`tf-postgres`, `tf-redis`)

Тот же пересбор стенда дал сервисам зависимостей новые хостнеймы с префиксом `tf-` (`tf-postgres`,
`tf-kafka`, `tf-rabbit`, `tf-redis` — `tf-redis` использовался и раньше, при добавлении аудита).
Обновили `DB_HOST` в обоих `.env.example` и в `scripts/seed-via-vault.sh` с `postgres` на `tf-postgres`;
`REDIS_HOST=tf-redis:6379` в `docker-compose.yml` уже был верным, не трогали.

## `DB_SKIP_SCHEMA_CREATE` — не дефолт в `.env.example`, а зашитый `"true"` в compose

Раньше флаг был `false` по умолчанию, потому что было неизвестно заранее, у кого будет `CREATE` на базу
— миграционная учётка могла либо сама создавать схему, либо только работать внутри уже готовой. Новая
инфра-спецификация делает это утверждение однозначным: схему, базу и роли (`bff_admin`/`bff_user`)
заводит инфраструктура, `bff_admin` — только владелец схемы (создаёт/меняет таблицы внутри неё), и
миграции не должны даже пытаться создавать схему.

Первая попытка — сменить дефолт в `deploy/migration/.env.example` на `true` — оказалась недостаточной на
практике: реальный `.env` на сервере остался со старым `false` (тем же способом, каким до этого там
застоялись старые значения `DOCKER_NETWORK` и `DB_MIGRATION_USER`), и миграция упала `permission denied
for database tf` на `CREATE SCHEMA IF NOT EXISTS`. Раз значение — не «настройка стенда», а архитектурный
факт (инфра всегда владеет схемой, всегда, на любом стенде), перенесли его из `.env.example` прямо в
`environment:` блок `docker-compose.migrations.yml` как `"true"` — `environment:` в compose всегда
сильнее `env_file:`, так что теперь это верно независимо от содержимого `.env` на сервере. Тот же приём,
которым до этого закрыли `DOCKER_NETWORK`/`think-fast-net`: то, что не должно отличаться между
окружениями, не должно жить в редактируемом вручную файле.

## `bff_admin`/`bff_user` — единственные роли, которыми пользуется tf-bff; `tf` (суперпользователь) — нет

Реальный сбой миграций на dev — `password authentication failed for user "tf"` — стоил отдельного
уточнения от инфраструктуры (docs/VAULT_MIGRATION.md, «Разбор реального сбоя миграций на dev»): `tf` —
не опечатка и не имя базы, а настоящая роль-суперпользователь Postgres, которой инфраструктура заводит
схему/роли/права заранее. Ни миграции, ни приложение не должны и не могут её использовать — если где-то
всплывает подключение под `tf`, это всегда значит, что `DB_MIGRATION_USER`/`DB_USER` в `.env` на сервере
не обновлён до `bff_admin`/`bff_user`, а не проблема кода или шаблонов репозитория (они уже верны с
самого переименования, см. запись про переименование плейсхолдеров выше).
