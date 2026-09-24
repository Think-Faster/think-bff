# Интеграция think-front с BFF

Документ для агента, дорабатывающего фронтенд (`think-front`). Самодостаточен — описывает всё, что нужно
знать про BFF, чтобы писать запросы, не читая исходники сервиса. Более короткая справка по путям — в
[`API.md`](API.md); здесь то же самое плюс TypeScript-типы и практические указания по интеграции.

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
  заводить свои через `POST /resources` (например `documents` для будущих доменных сущностей).

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
  isActive: boolean;
  groups: GroupRefDto[]; // только группы первого уровня (без вложенности)
}

interface UserListItemDto {
  id: string;
  authUserId: string;
  lastName: string;
  firstName: string;
  middleName: string | null;
  isActive: boolean;
}

interface CreateUserRequest {
  authUserId: string;
  lastName: string;
  firstName: string;
  middleName?: string | null;
  groupIds?: string[]; // опционально сразу включить в группы при создании
}

interface UpdateUserRequest {
  lastName: string;
  firstName: string;
  middleName?: string | null;
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

## 8. Доменные сущности (объекты, датчики, прогнозы, заявки, админ-настройки)

Новый пласт API поверх модели из `домены-и-сущности.md` — коды ресурсов и их владение описаны в
артефакте «Межсервисная модель данных think-front» (там же — что осталось у `tf-funnel`/`tf-model`, а что
теперь в BFF). Общие соглашения (camelCase, `ErrorResponse`, `PagedResult<T>`, коды ошибок, гейтинг через
`/permissions/me`) — те же, разделы 3–4 выше.

### 8.1 TypeScript-типы

```ts
// ---- Объекты и топология ----

type ObjectStatus = "normal" | "watch" | "alarm" | "offline";

interface ObjectDto {
  id: number; // внешний id из справочника системы мониторинга, не генерируется BFF
  level: number;
  parentId: number | null;
  kind: string;
  name: string;
  address: string | null;
  geometryGeoJson: string | null;
  status: ObjectStatus;
  statusAt: string; // ISO datetime
}

interface CreateObjectRequest {
  id: number;
  level: number;
  parentId?: number | null;
  kind: string;
  name: string;
  address?: string | null;
  geometryGeoJson?: string | null;
}

interface UpdateObjectRequest {
  name: string;
  address?: string | null;
  geometryGeoJson?: string | null;
}

interface PicketDto {
  id: string; // long, но в JSON приходит строкой/числом — сверься на практике с сериализацией int64
  objectId: number;
  code: string;
  ordinal: number;
  geometryGeoJson: string | null;
}

interface CreatePicketRequest {
  code: string;
  ordinal: number;
  geometryGeoJson?: string | null;
}

interface UpdatePicketRequest {
  ordinal: number;
  geometryGeoJson?: string | null;
}

interface MapLayerDto {
  id: string;
  level: number; // 1 город, 2 объект, 3 датчики, 4 внутренняя схема
  objectId: number | null;
  kind: string;
  geoJson: string;
  updatedAt: string;
}

interface UpsertMapLayerRequest {
  level: number;
  kind: string;
  geoJson: string;
}

// ---- Датчики ----

interface SensorDto {
  id: number; // внешний "ид_канала_данных"
  objectId: number;
  picketId: string | null;
  system: string;
  sType: string;
  tag: string | null;
  name: string;
  isActive: boolean;
  // Текущее значение/состояние здесь НЕТ — это снимок потока tf-funnel, не карточка датчика.
}

interface CreateSensorRequest {
  id: number;
  objectId: number;
  picketId?: string | null;
  system: string;
  sType: string;
  tag?: string | null;
  name: string;
}

interface UpdateSensorRequest {
  picketId?: string | null;
  name: string;
  tag?: string | null;
  isActive: boolean;
}

interface SensorLinkDto {
  fromSensorId: number;
  toSensorId: number;
  kind: string;
}

interface CreateSensorLinkRequest {
  toSensorId: number;
  kind: string;
}

// ---- Прогнозы ----

type PredictionType = "fire" | "gas" | "flood" | "equipmentFailure" | "sensorFailure" | "intrusion";
type PredictionStatus = "new" | "inReview" | "taken" | "rejected" | "muted" | "closed";
type DecisionAction = "take" | "reject" | "mute" | "reopen";

interface PredictionListItemDto {
  id: string;
  objectId: number;
  type: PredictionType;
  topic: string;
  probability: number; // 0..1
  hourEnd: string;
  sinceHours: number;
  status: PredictionStatus;
}

interface PredictionFactorDto {
  feature: string;
  value: number;
  weight: number;
  direction: string;
}

interface PredictionEvidenceDto {
  sensorId: number;
  picketId: string | null;
  ts: string;
  value: number | null;
}

interface PredictionDto {
  id: string;
  objectId: number;
  type: PredictionType;
  hourEnd: string;
  horizonHours: number;
  score: number;
  threshold: number;
  alarm: boolean;
  probability: number;
  confidence: number;
  sinceHours: number;
  topic: string;
  description: string | null;
  classification: string | null;
  recommendation: string | null;
  modelVersionId: string | null;
  status: PredictionStatus;
  mutedReason: string | null;
  factors: PredictionFactorDto[];
  evidence: PredictionEvidenceDto[];
}

interface PredictionDecisionDto {
  id: string;
  predictionId: string;
  userId: string;
  action: DecisionAction;
  reasonCode: string | null;
  comment: string | null;
  taskId: string | null;
  decidedAt: string;
}

interface CreatePredictionDecisionRequest {
  action: DecisionAction;
  reasonCode?: string | null; // обязателен при action === "reject"
  comment?: string | null;
}

interface FactAlertDto {
  id: string;
  objectId: number;
  type: PredictionType;
  startedAt: string;
  announcedAt: string;
  triggerSensorIds: number[];
  status: string;
}

// ---- Заявки и работы ----

type TaskSourceType = "prediction" | "fact" | "call" | "external";
type WorkTaskStatus =
  | "new" | "inWork" | "assigned" | "engineerWorking"
  | "completed" | "closed" | "returnedToWork" | "cancelled";

interface WorkTaskListItemDto {
  id: string;
  number: string;
  sourceType: TaskSourceType;
  objectId: number;
  topic: string;
  status: WorkTaskStatus;
  dispatcherId: string | null;
  createdAt: string;
}

interface TaskPredictionDto {
  predictionId: string;
  attachedBy: string;
  attachedAt: string;
  detachedAt: string | null;
  isPrimary: boolean;
}

interface TaskAssignmentDto {
  id: string;
  engineerId: string;
  assignedBy: string;
  assignedAt: string;
  status: string;
  comment: string | null;
}

interface TaskReportDto {
  id: string;
  engineerId: string;
  actualState: string | null;
  worksDone: string | null;
  resultCode: string;
  comment: string | null;
  createdAt: string;
}

interface TaskReturnDto {
  id: string;
  returnedBy: string;
  targetType: "dispatcher" | "queue" | "incident";
  targetUserId: string | null;
  comment: string | null;
  returnedAt: string;
}

interface WorkTaskDto {
  id: string;
  number: string;
  sourceType: TaskSourceType;
  objectId: number;
  picketId: string | null;
  topic: string;
  description: string | null;
  workType: string | null;
  faultClassification: string | null;
  sensorIds: number[];
  comment: string | null;
  dispatcherId: string | null;
  status: WorkTaskStatus;
  priority: number;
  createdAt: string;
  takenAt: string | null;
  assignedAt: string | null;
  completedAt: string | null;
  closedAt: string | null;
  predictions: TaskPredictionDto[];
  assignments: TaskAssignmentDto[];
  reports: TaskReportDto[];
  returns: TaskReturnDto[];
}

interface CreateWorkTaskRequest {
  number: string;
  sourceType: TaskSourceType;
  objectId: number;
  picketId?: string | null;
  topic: string;
  description?: string | null;
  workType?: string | null;
  faultClassification?: string | null;
  sensorIds?: number[];
  priority: number;
}

interface UpdateWorkTaskRequest {
  topic: string;
  description?: string | null;
  workType?: string | null;
  faultClassification?: string | null;
  comment?: string | null;
  priority: number;
}

interface IncidentDto {
  id: string;
  objectId: number;
  type: PredictionType;
  startedAt: string;
  confirmedAt: string | null;
  confirmedBy: string | null;
  taskId: string | null;
  predictionId: string | null;
  outcome: string | null;
}

// ---- Люди: график, закреплённые объекты, присутствие, инженеры ----

type ScheduleStatus = "working" | "notWorking" | "onLeave";

interface ScheduleEntryDto {
  id: string;
  userId: string;
  dateFrom: string; // "YYYY-MM-DD"
  dateTo: string;
  status: ScheduleStatus;
  source: string | null;
  changedBy: string;
  changedAt: string;
}

interface AssignedObjectDto {
  userId: string;
  objectId: number;
  assignedBy: string;
  assignedAt: string;
  note: string | null;
}

interface PresenceDto {
  userId: string;
  isOnline: boolean; // last_seen_at не старше PRESENCE_ONLINE_WINDOW (по умолчанию 5 минут)
  lastSeenAt: string | null;
  lastAction: string | null;
}

type EngineerStatus = "available" | "assigned" | "busy" | "unavailable";

interface BrigadeDto {
  id: string;
  name: string;
}

interface EngineerProfileDto {
  userId: string;
  brigadeId: string | null;
  phone: string | null;
  telegram: string | null;
  specialization: string[];
  status: EngineerStatus;
}

// ---- Админ-настройки модели (D8) ----

interface ModelVersionDto {
  id: string;
  name: string;
  isDefault: boolean;
  switchedAt: string | null;
  switchedBy: string | null;
  createdAt: string;
}

interface CoefficientDto {
  id: string;
  type: PredictionType;
  share: number; // 0..1, доля объекто-часов под тревогой
  rejectK: number | null;
  version: number; // растёт на каждую правку, история не удаляется
  createdBy: string;
  createdAt: string;
  reason: string | null;
}

interface RetrainJobDto {
  id: string;
  requestedBy: string;
  requestedAt: string;
  paramsJson: string | null;
  status: string;
  startedAt: string | null;
  finishedAt: string | null;
  resultModelVersionId: string | null;
  logRef: string | null;
}

type IgnoredRangeScope = "all" | "object" | "sensor";

interface IgnoredRangeDto {
  id: string;
  scope: IgnoredRangeScope;
  objectId: number | null;
  sensorId: number | null;
  dateFrom: string;
  dateTo: string;
  reason: string;
  createdBy: string;
  createdAt: string;
}
```

### 8.2 Справочник эндпоинтов

#### Объекты

| Метод | Путь | Право | Ответ |
| --- | --- | --- | --- |
| GET | `/objects?search=&page=&pageSize=` | `objects:read` | `PagedResult<ObjectDto>` |
| GET | `/objects/{id}` | `objects:read` | `ObjectDto` |
| POST | `/objects` | `objects:create` | `201` + `ObjectDto`. `id` — внешний, не генерируется |
| PUT | `/objects/{id}` | `objects:update` | `ObjectDto` |
| DELETE | `/objects/{id}` | `objects:delete` | `204` |
| GET | `/objects/{id}/pickets` | `objects:read` | `PicketDto[]` |
| POST | `/objects/{id}/pickets` | `objects:update` | `201` + `PicketDto`. Дубль `code` на объекте → `409` |
| PUT | `/objects/{id}/pickets/{picketId}` | `objects:update` | `PicketDto` |
| DELETE | `/objects/{id}/pickets/{picketId}` | `objects:update` | `204` |
| GET | `/objects/{id}/layers?level=` | `objects:read` | `MapLayerDto[]` |
| POST | `/objects/{id}/layers` | `objects:update` | upsert по `(objectId, level, kind)` — `MapLayerDto` |

#### Датчики

| Метод | Путь | Право | Ответ |
| --- | --- | --- | --- |
| GET | `/sensors?objectId=&search=&page=&pageSize=` | `sensors:read` | `PagedResult<SensorDto>` |
| GET | `/sensors/{id}` | `sensors:read` | `SensorDto` |
| POST | `/sensors` | `sensors:create` | `201` + `SensorDto`. `id` — внешний |
| PUT | `/sensors/{id}` | `sensors:update` | `SensorDto` |
| DELETE | `/sensors/{id}` | `sensors:delete` | `204` |
| GET | `/sensors/{id}/links` | `sensors:read` | `SensorLinkDto[]` |
| POST | `/sensors/{id}/links` | `sensors:update` | `201` + `SensorLinkDto` |
| DELETE | `/sensors/{id}/links/{toSensorId}/{kind}` | `sensors:update` | `204` |

#### Прогнозы

| Метод | Путь | Право | Ответ |
| --- | --- | --- | --- |
| GET | `/predictions?objectId=&status=&page=&pageSize=` | `predictions:read` | `PagedResult<PredictionListItemDto>` |
| GET | `/predictions/{id}` | `predictions:read` | `PredictionDto` (с `factors`/`evidence`) |
| POST | `/predictions` | `predictions:create` | `201` + `PredictionDto`. Дедуп по `(objectId, type, hourEnd, modelVersionId)` — повтор вернёт уже существующий, не создаст дубль |
| POST | `/predictions/{id}/decisions` | `predictions:update` | `200` + `PredictionDecisionDto`. Меняет `status` прогноза (`take`→`taken`, `reject`→`rejected`, `mute`→`muted`, `reopen`→`inReview`) |
| GET | `/fact-alerts?objectId=&page=&pageSize=` | `predictions:read` | `PagedResult<FactAlertDto>` |
| POST | `/fact-alerts` | `predictions:create` | `201` + `FactAlertDto` |

#### Заявки и работы

| Метод | Путь | Право | Ответ |
| --- | --- | --- | --- |
| GET | `/tasks?dispatcherId=&status=&page=&pageSize=` | `tasks:read` | `PagedResult<WorkTaskListItemDto>` |
| GET | `/tasks/{id}` | `tasks:read` | `WorkTaskDto` (с вложенными `predictions`/`assignments`/`reports`/`returns`) |
| POST | `/tasks` | `tasks:create` | `201` + `WorkTaskDto`. Дубль `number` → `409 duplicate_code` |
| PUT | `/tasks/{id}` | `tasks:update` | `WorkTaskDto` |
| POST | `/tasks/{id}/take` | `tasks:update` | «кто первый взял — тот ведёт»: `200` + `WorkTaskDto`, либо `409 task_already_taken`, если кто-то успел раньше — **обязательно обработай этот код отдельно от прочих 409** (не ошибка данных, а гонка) |
| POST | `/tasks/{id}/predictions` | `tasks:update` | тело `{ predictionId, isPrimary }` — прикрепить прогноз как основание, `201` |
| DELETE | `/tasks/{id}/predictions/{predictionId}` | `tasks:update` | открепить (запись остаётся в истории с `detachedAt`) — `204` |
| POST | `/tasks/{id}/assignments` | `tasks:update` | тело `{ engineerId, comment? }` — `201` + `TaskAssignmentDto` |
| POST | `/tasks/{id}/reports` | `tasks:update` | тело `CreateTaskReportRequest`-подобное (`actualState?`, `worksDone?`, `resultCode`, `comment?`) — `201` + `TaskReportDto`, заявка переходит в `completed` |
| POST | `/tasks/{id}/returns` | `tasks:update` | тело `{ targetType, targetUserId?, comment? }` — `201` + `TaskReturnDto`, заявка переходит в `returnedToWork` |

#### Происшествия

| Метод | Путь | Право | Ответ |
| --- | --- | --- | --- |
| GET | `/incidents?objectId=&page=&pageSize=` | `incidents:read` | `PagedResult<IncidentDto>` |
| GET | `/incidents/{id}` | `incidents:read` | `IncidentDto` |
| POST | `/incidents` | `incidents:create` | `201` + `IncidentDto` |
| POST | `/incidents/{id}/confirm` | `incidents:update` | тело `{ outcome? }` — `200` + `IncidentDto` с `confirmedAt`/`confirmedBy` |

#### График, закреплённые объекты, присутствие, инженеры (все — под `/users/{id}/...`, кроме `/presence` и `/brigades`)

| Метод | Путь | Право | Ответ |
| --- | --- | --- | --- |
| GET | `/users/{id}/schedule` | `schedule:read` | `ScheduleEntryDto[]` |
| POST | `/users/{id}/schedule` | `schedule:update` | `201` + `ScheduleEntryDto` |
| DELETE | `/users/{id}/schedule/{entryId}` | `schedule:update` | `204` |
| GET | `/users/{id}/assigned-objects` | `assigned_objects:read` | `AssignedObjectDto[]` |
| POST | `/users/{id}/assigned-objects` | `assigned_objects:update` | тело `{ objectId, note? }` — upsert по `(userId, objectId)`, `201` |
| DELETE | `/users/{id}/assigned-objects/{objectId}` | `assigned_objects:update` | `204` |
| GET | `/users/{id}/engineer-profile` | `engineers:read` | `EngineerProfileDto` либо `404`, если профиля ещё нет |
| PUT | `/users/{id}/engineer-profile` | `engineers:update` | upsert — `EngineerProfileDto` |
| GET | `/presence?userIds=id1&userIds=id2` | `presence:read` | `PresenceDto[]`. Без `userIds` — все. Только чтение: пишет присутствие сам BFF на каждый аутентифицированный запрос, отдельной ручки записи нет |
| GET | `/brigades` | `engineers:read` | `BrigadeDto[]` |
| POST | `/brigades` | `engineers:create` | `201` + `BrigadeDto` |

#### Админ-настройки модели

Все ручки — под правом `model_settings` (`read` для GET, `manage` для любых изменений — единый уровень,
без отдельных `create`/`update`/`delete`, как и у `permissions`).

| Метод | Путь | Право | Ответ |
| --- | --- | --- | --- |
| GET | `/model-versions` | `model_settings:read` | `ModelVersionDto[]` |
| POST | `/model-versions` | `model_settings:manage` | `201` + `ModelVersionDto`. Дубль `id` → `409 duplicate_code` |
| POST | `/model-versions/{id}/activate` | `model_settings:manage` | делает версию `isDefault`, снимает флаг со всех остальных — `ModelVersionDto` |
| GET | `/coefficients` | `model_settings:read` | `CoefficientDto[]` — по одной, последней версии на каждый `type` |
| POST | `/coefficients` | `model_settings:manage` | `201` + `CoefficientDto`. **Не апдейт** — каждая правка создаёт новую версию, старые остаются в истории |
| GET | `/retrain-jobs` | `model_settings:read` | `RetrainJobDto[]` |
| POST | `/retrain-jobs` | `model_settings:manage` | `201` + `RetrainJobDto` со `status: "requested"` — сам запуск переобучения делает `tf-model`, BFF только фиксирует заявку |
| GET | `/ignored-ranges` | `model_settings:read` | `IgnoredRangeDto[]` |
| POST | `/ignored-ranges` | `model_settings:manage` | `201` + `IgnoredRangeDto`. `objectId` обязателен при `scope: "object"`, `sensorId` — при `scope: "sensor"` |
| DELETE | `/ignored-ranges/{id}` | `model_settings:manage` | `204` |

### 8.3 На что обратить внимание

- **`ObjectDto.id`/`SensorDto.id` — не BFF-шные UUID, а внешние числовые ID** из справочника системы
  мониторинга. Создание объекта/датчика (`POST /objects`, `POST /sensors`) требует передать `id` явно —
  это не автогенерируемое поле, как у остальных сущностей в этом API.
- **`Prediction`/`FactAlert` в норме создаёт не человек, а модель** (через пока не построенный Kafka-
  consumer `tf.forecast.results`). `POST /predictions`/`POST /fact-alerts` уже работают и защищены правами
  — годятся для тестовых сценариев и админки, но не жди, что это будет типовая форма создания в UI
  диспетчера.
- **`POST /tasks/{id}/take` — гонка, не обычная валидация.** Два диспетчера могут одновременно нажать
  «Взять в работу» на одной заявке; `409 task_already_taken` — штатный, ожидаемый исход для одного из
  них, не баг. Обработай отдельным сообщением («заявку уже взяли») и обнови список.
- **`Coefficient` версионируется, не редактируется.** `POST /coefficients` всегда создаёт новую строку;
  `GET /coefficients` отдаёт только последнюю версию на каждый тип — историю (для объяснения, «почему
  поток тревог отличался в прошлом месяце») отдельной ручки пока нет, читай из БД/просьба к бэкенду.
- **Присутствие не имеет ручки записи.** `/presence` — только чтение; отметка `lastSeenAt` ставится сама
  на бэкенде при каждом аутентифицированном запросе (не чаще раза в 30 сек на юзера). Отдельный «пинг
  присутствия» с фронта не нужен и не будет учтён.
