# Интеграция think-front с BFF — пользователи, группы, права

Документ для агента, дорабатывающего фронтенд (`think-front`). Самодостаточен — описывает всё, что нужно
знать про базовый RBAC-контур BFF (`users`/`groups`/`permissions`), чтобы писать запросы, не читая
исходники сервиса. Более короткая справка по путям — в [`API.md`](API.md); здесь то же самое плюс
TypeScript-типы и практические указания по интеграции.

Доменные сущности поверх этого контура (объекты, датчики, прогнозы, заявки, график/присутствие/инженеры,
админ-настройки модели) — отдельный документ,
[`FRONTEND_INTEGRATION_DOMAIN_MODELS.md`](FRONTEND_INTEGRATION_DOMAIN_MODELS.md). Общие соглашения
(раздел 1–4 ниже — базовый путь, авторизация, формат ошибок, гейтинг прав) одинаковы для обоих
документов, там не повторяются.

## 1. Как ходить к BFF

Снаружи (из браузера) BFF доступен только через nginx, под префиксом `/api/bff/`:

```
https://greefob.ru/api/bff/users
https://greefob.ru/api/bff/groups/{id}
https://greefob.ru/api/bff/permissions/me
```

**Важно:** фронтенд (`tf-front`) и BFF сидят за одним и тем же nginx/доменом. Значит, запросы с фронта
нужно делать **относительным путём** (`/api/bff/...` от `window.location.origin`), а не абсолютным URL на
другой домен/порт. Тогда:
- не нужен CORS вообще (same-origin) — `CORS_ALLOWED_ORIGINS` на бэкенде можно оставить пустым;
- куки (см. ниже про авторизацию) браузер отправит сам, без специальной настройки domain/SameSite.

Все пути в разделах 3–7 этого документа — то, что идёт **после** `/api/bff/` (внутренний путь BFF, без
`/api`). Пример: строка "`GET /users/{id}`" в таблице ниже на практике — это
`GET https://greefob.ru/api/bff/users/{id}`.

nginx срезает `/api/bff/` перед проксированием на сам сервис — у контроллеров BFF в коде нет и не должно
быть префикса `/api`.

## 2. Авторизация — что должен делать фронтенд

**Логин/логаут/регистрация — не в BFF.** Это отдельный сервис аутентификации (`tf-auth`, снаружи —
`/api/auth/...`). BFF только читает уже выставленный токен и отдаёт бизнес-данные с проверкой прав.

Что нужно от фронтенда:

1. **Слать куки с каждым запросом к BFF.** `fetch(url, { credentials: 'include' })` или, если на axios,
   `axios.create({ withCredentials: true })`. Токен доступа лежит в httpOnly-куке (имя по умолчанию
   `access_token` — управляется сервисом аутентификации), JS до неё не достаёт и не должен пытаться —
   всё через `credentials`/`withCredentials`.
2. **Не реализовывать логику обновления токена самостоятельно.** Если access-токен истёк, BFF сам
   обращается к `tf-auth` за новым и прозрачно проставляет обновлённые `Set-Cookie` в свой же ответ —
   для фронтенда это выглядит как обычный успешный запрос (или как явная ошибка ниже, если рефреш не
   удался).
3. **Разный смысл у разных кодов ошибок авторизации** (тело `{ code, message, details }`, HTTP-статус
   рядом):

   | `code` | HTTP | Что значит для UI |
   | --- | --- | --- |
   | `unauthenticated` | 401 | Токена нет вообще → на экран логина. |
   | `invalid_token` | 401 | Токен битый/не тот issuer → на экран логина. |
   | `token_refresh_failed` | 401 | Токен истёк, обновить не вышло → на экран логина. |
   | `auth_service_unavailable` | 503 | Инфраструктурная проблема (сервис аутентификации недоступен) — **не** логин-проблема; показать баннер "сервис временно недоступен, попробуйте позже", не редиректить на логин. |
   | `user_not_provisioned` | 403 | Юзер прошёл логин в `tf-auth`, но не заведён в BFF (нет записи в `users`) — отдельный экран "обратитесь к администратору", не логин. |
   | `user_inactive` | 403 | Учётка деактивирована в BFF — тоже отдельный экран, не логин. |
   | `permission_denied` | 403 | Обычный отказ RBAC — см. раздел 5 про гейтинг UI. |

   Практический совет: в общем http-клиенте фронта различай `401` (→ редирект на логин) от `403` с
   `user_not_provisioned`/`user_inactive` (→ информационный экран) от `403` с `permission_denied`
   (→ либо скрыть кнопку заранее через `/permissions/*`, либо показать тост "недостаточно прав") от `503`
   (→ баннер о временной недоступности, с ретраем).

## 3. Общие соглашения по ответам

- **JSON, camelCase везде** — и у успешных ответов, и у тела ошибок (это гарантировано на уровне бэкенда,
  единообразно).
- **Формат ошибки одинаковый на всех эндпоинтах:**

```ts
interface ErrorResponse {
  code: string;
  message: string;
  details: Record<string, string[]> | null; // непустой только у 400 validation_failed — ключ: имя поля запроса (camelCase), значение: список сообщений валидации
}
```

- **Пагинация** — везде одинаковой формы:

```ts
interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}
```

- **Коды ошибок целиком** (для маппинга на UI-сообщения):

| `code` | HTTP | Когда |
| --- | --- | --- |
| `unauthenticated` | 401 | нет токена |
| `invalid_token` | 401 | токен битый / нет нужного claim'а |
| `token_refresh_failed` | 401 | истёк, рефреш не удался |
| `auth_service_unavailable` | 503 | не удалось получить ключ подписи токена — временная инфраструктурная проблема |
| `user_not_provisioned` | 403 | юзера нет в BFF |
| `user_inactive` | 403 | юзер деактивирован |
| `permission_denied` | 403 | не хватает права |
| `not_found` | 404 | сущность не найдена |
| `cycle_detected` | 409 | добавление участника группы замкнуло бы цикл вложенности |
| `duplicate_code` | 409 | код группы/ресурса уже занят |
| `system_group_protected` | 409 | попытка удалить системную группу |
| `validation_failed` | 400 | тело запроса не прошло валидацию — смотри `details` |
| `bad_request` | 400 | некорректный аргумент (например неизвестный `memberType`/`principalType`/имя права) |
| `internal_error` | 500 | необработанная ошибка на бэкенде |

## 4. Модель прав (RBAC) — что нужно понимать перед вёрсткой UI

- Принципал (у кого могут быть права) — **пользователь** или **группа**. Пользователь может состоять в
  нескольких группах, группы могут быть вложены друг в друга.
- Право на ресурс — набор из фиксированного списка имён:

```ts
type PermissionName = "create" | "read" | "update" | "delete" | "export" | "import" | "manage";
```

  `manage` — надправо: даёт вообще любую операцию над ресурсом, в том числе выдачу прав другим на этот же
  ресурс. Если у юзера есть `manage` на `documents`, относись к этому как к полному доступу, даже если в
  списке явно нет `read`/`update` и т.д.

- Предопределённые (всегда существующие) коды ресурсов: `users`, `groups`, `permissions`. Дальше можно
  заводить свои через `POST /resources` — уже заведены `objects`, `sensors`, `predictions`, `tasks`,
  `incidents`, `schedule`, `assigned_objects`, `engineers`, `presence`, `model_settings` (см.
  `FRONTEND_INTEGRATION_DOMAIN_MODELS.md`).

- **Как гейтить UI:**
  1. При старте приложения (после логина) дёрни `GET /permissions/me` — получишь карту "ресурс → права"
     для текущего юзера. Держи это в глобальном сторе, используй для скрытия пунктов меню, кнопок
     создания/удаления и т.д.
  2. Для точечной проверки прямо перед действием (или если карта могла устареть) — `GET
     /permissions/check?resource=...&permission=...`. Дешёвая операция (бэкенд кэширует права в памяти).
  3. В любом случае бэкенд перепроверяет права на каждый запрос сам (`403 permission_denied`, если
     что-то не сошлось) — гейтинг на фронте это только UX, не полагайся на него как на единственный
     барьер.

## 5. TypeScript-типы всех DTO

```ts
// ---- Common ----

interface GroupRefDto {
  id: string; // uuid
  code: string;
  name: string;
}

// ---- Users ----

interface UserDto {
  id: string;
  authUserId: string;
  lastName: string;
  firstName: string;
  middleName: string | null;
  email: string | null; // для POST /notifications/email по userId — см. FRONTEND_INTEGRATION_NOTIFICATIONS.md
  isActive: boolean;
  groups: GroupRefDto[]; // только группы первого уровня (без вложенности)
}

interface UserListItemDto {
  id: string;
  authUserId: string;
  lastName: string;
  firstName: string;
  middleName: string | null;
  email: string | null;
  isActive: boolean;
}

interface CreateUserRequest {
  authUserId: string;
  lastName: string;
  firstName: string;
  middleName?: string | null;
  email?: string | null;
  groupIds?: string[]; // опционально сразу включить в группы при создании
}

interface UpdateUserRequest {
  lastName: string;
  firstName: string;
  middleName?: string | null;
  email?: string | null;
  isActive: boolean;
  // groupIds сюда не входит — состав групп меняется отдельными эндпоинтами
}

interface AddUserGroupsRequest {
  groupIds: string[];
}

// ---- Groups ----

type MemberType = "user" | "group";

interface GroupMemberDto {
  type: MemberType;
  id: string;
  displayName: string;
  code?: string | null; // заполнено только когда type === "group"
}

interface GroupDto {
  id: string;
  code: string;
  name: string;
  isSystem: boolean; // системные группы (сейчас — "admins") нельзя удалить
  members: GroupMemberDto[]; // только участники первого уровня, юзеры и группы вперемешку
}

interface GroupListItemDto {
  id: string;
  code: string;
  name: string;
  isSystem: boolean;
}

interface CreateGroupRequest {
  code: string; // ^[a-z0-9_-]+$, уникален
  name: string;
}

interface UpdateGroupRequest {
  name: string; // code через этот эндпоинт не меняется вообще
}

interface AddGroupMemberRequest {
  memberId: string;
  memberType: MemberType;
}

interface AddGroupMembersBatchRequest {
  members: AddGroupMemberRequest[];
}

// ---- Permissions ----

type PermissionName = "create" | "read" | "update" | "delete" | "export" | "import" | "manage";
type PrincipalType = "user" | "group";

interface MyPermissionsResponse {
  userId: string;
  permissions: Record<string, PermissionName[]>; // код ресурса -> список прав
  groups: string[]; // коды групп пользователя: прямые и родительские (роли — это группы)
}

interface CheckPermissionResponse {
  allowed: boolean;
}

interface GrantDto {
  id: string;
  principalType: PrincipalType;
  principalId: string;
  resourceCode: string;
  permissions: PermissionName[];
}

interface CreateGrantRequest {
  principalType: PrincipalType;
  principalId: string;
  resourceCode: string;
  permissions: PermissionName[]; // ПОЛНОСТЬЮ заменяет текущую маску для этой пары принципал+ресурс, не складывает
}

interface ResourceDto {
  id: number;
  code: string;
  name: string;
}

interface CreateResourceRequest {
  code: string; // ^[a-z0-9_-]+$, уникален
  name: string;
}
```

## 6. Справочник эндпоинтов

Путь без `/api/bff` — добавляй его сам (см. раздел 1). Колонка **Право** — что нужно иметь, чтобы получить
`200` вместо `403 permission_denied` (не влияет на аутентификацию — она нужна всегда).

### Пользователи

| Метод | Путь | Query / body | Право | Ответ |
| --- | --- | --- | --- | --- |
| GET | `/users` | `?search=&page=1&pageSize=50` | `users:read` | `PagedResult<UserListItemDto>` |
| GET | `/users/{id}` | — | `users:read` | `UserDto` |
| POST | `/users` | body `CreateUserRequest` | `users:create` | `201` + `UserDto` |
| PUT | `/users/{id}` | body `UpdateUserRequest` | `users:update` | `UserDto` |
| DELETE | `/users/{id}` | `?soft=true` (по умолчанию) | `users:delete` | `204`. `soft=true` — деактивация (`isActive=false`); `soft=false` — удаление насовсем вместе с членствами и грантами |
| POST | `/users/{id}/groups` | body `AddUserGroupsRequest` | `users:update` | `200` + `GroupRefDto[]` (актуальный список групп первого уровня). Дубли молча игнорируются, несуществующая группа → `404` |
| DELETE | `/users/{id}/groups/{groupId}` | — | `users:update` | `204` |

Про `/users/{id}/schedule`, `/users/{id}/assigned-objects`, `/users/{id}/engineer-profile` — см.
`FRONTEND_INTEGRATION_DOMAIN_MODELS.md` (это доменные дополнения, а не базовый RBAC).

### Группы

| Метод | Путь | Query / body | Право | Ответ |
| --- | --- | --- | --- | --- |
| GET | `/groups` | `?search=&page=&pageSize=` | `groups:read` | `PagedResult<GroupListItemDto>` |
| GET | `/groups/{id}` | — | `groups:read` | `GroupDto` |
| POST | `/groups` | body `CreateGroupRequest` | `groups:create` | `201` + `GroupDto`. Дубль `code` → `409 duplicate_code` |
| PUT | `/groups/{id}` | body `UpdateGroupRequest` | `groups:update` | `GroupDto` |
| DELETE | `/groups/{id}` | — | `groups:delete` | `204`. Системную группу удалить нельзя → `409 system_group_protected` |
| POST | `/groups/{id}/members` | body `AddGroupMemberRequest` | `groups:update` | `201`. Цикл вложенности → `409 cycle_detected` |
| POST | `/groups/{id}/members/batch` | body `AddGroupMembersBatchRequest` | `groups:update` | `201`. Вся пачка — одна транзакция, любой цикл откатывает всё |
| DELETE | `/groups/{id}/members/{memberType}/{memberId}` | `memberType`: `user`\|`group` | `groups:update` | `204` |

### Права и ресурсы

| Метод | Путь | Query / body | Право | Ответ |
| --- | --- | --- | --- | --- |
| GET | `/permissions/me` | — | только аутентификация | `MyPermissionsResponse` |
| GET | `/permissions/check` | `?resource=&permission=` | только аутентификация | `CheckPermissionResponse` |
| GET | `/permissions/grants` | `?principalType=&principalId=` (оба опциональны, для фильтра) | `permissions:read` | `GrantDto[]` |
| POST | `/permissions/grants` | body `CreateGrantRequest` | `permissions:manage` | `200` + `GrantDto` (upsert — маска перезаписывается целиком) |
| DELETE | `/permissions/grants/{id}` | — | `permissions:manage` | `204` |
| GET | `/resources` | — | только аутентификация | `ResourceDto[]` |
| POST | `/resources` | body `CreateResourceRequest` | `permissions:manage` | `201` + `ResourceDto`. Дубль `code` → `409 duplicate_code` |

### Служебное

| Метод | Путь | Право | Ответ |
| --- | --- | --- | --- |
| GET | `/health/live` | нет | `{ status: "ok" }` — жив ли процесс |
| GET | `/health/ready` | нет | `{ status, database, jwks }`, `503` если что-то не готово. Фронту, скорее всего, не нужен напрямую. |

## 7. Разное, на что стоит обратить внимание при вёрстке форм

- **ФИО** — `lastName`/`firstName` обязательны (`NOT NULL` в БД), `middleName` — нет.
- **`code` у групп/ресурсов** — латиница в нижнем регистре, цифры, `-`/`_` (`^[a-z0-9_-]+$`), уникален.
  После создания у групп код сменить нельзя (`PUT /groups/{id}` принимает только `name`).
  Дубликат кода → `409 duplicate_code`, показывай как ошибку конкретно поля `code`, а не общий тост.
- **Guid везде** — `id`, `principalId`, `memberId`, `groupId` и т.д. — обычные UUID-строки.
- **`PUT /users/{id}`** не меняет состав групп — это отдельные `POST/DELETE .../groups[...]`. Если в форме
  редактирования юзера есть блок "группы" — это отдельные запросы, не часть одного сохранения.
- **Upsert прав (`POST /permissions/grants`)** — не аддитивный. Если хочешь *добавить* одно право к уже
  выданным, сначала прочитай текущие через `GET /permissions/grants?principalType=...&principalId=...`,
  собери объединённый список и отправь его целиком — иначе затрёшь то, что было.
