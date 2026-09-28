# Интеграция think-front с BFF — доменные сущности

Документ для агента, дорабатывающего фронтенд (`think-front`). Покрывает доменный пласт API поверх
базового RBAC-контура: объекты и топология, датчики, прогнозы, заявки и работы, происшествия, график и
присутствие, инженеры и бригады, админ-настройки модели.

Общие соглашения — базовый путь через nginx, авторизация/куки, формат ошибок, `PagedResult<T>`, гейтинг
UI через `/permissions/me` — вынесены в отдельный документ,
[`FRONTEND_INTEGRATION_GROUPS_USERS_PERMISSIONS.md`](FRONTEND_INTEGRATION_GROUPS_USERS_PERMISSIONS.md),
разделы 1–4. Прочти их сначала, если ещё не работал с BFF — здесь эти правила не повторяются.

Владение сущностями по сервисам (что осталось у `tf-funnel`/`tf-model`, а что теперь в BFF) — в артефакте
«Межсервисная модель данных think-front» (там же вкладка «Диаграмма классов BFF» — как эти сущности
связаны на уровне кода, с указанием, где есть настоящий FK, а где просто поле-ссылка без FK).

## 1. TypeScript-типы

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

// Окно «Логи»: чьи показания видны. Сами показания — у tf-funnel (/api/funnel/log, /api/funnel/stream),
// он же и спрашивает эту ручку с токеном пользователя; фронту она нужна, только чтобы заранее знать,
// какие объекты инженер может открыть.
interface ReadingsScopeDto {
  all: boolean; // true — любой объект (право readings:read): диспетчер, главный, админ
  objectIds: number[]; // запрошенные и видимые; без запроса — объекты открытых заявок инженера
  sensorIds: number[]; // датчики этих объектов и их потомков
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
  taskId: string | null;      // take: заявка, которую завёл BFF или к которой прикрепил прогноз
  mutedUntil: string | null;  // mute: до какого времени молчит пара объект-тип
  decidedAt: string;
}

interface CreatePredictionDecisionRequest {
  action: DecisionAction;
  reasonCode?: string | null; // обязателен при action === "reject"; "other" — только с comment
  comment?: string | null;
  taskId?: string | null;     // take: прикрепить к уже открытой заявке того же объекта; пусто — новая заявка
  until?: string | null;      // обязателен при action === "mute", не раньше чем через час
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
  shiftStart: string | null; // "HH:mm:ss" — начало смены в каждый день интервала
  shiftHours: number | null; // 1..24; сутки через трое — shiftStart "08:00:00", shiftHours 24
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
  unit: string | null;     // эксплуатационное подразделение (участок, район)
  leaderId: string | null; // бригадир — userId; роль «бригадир» хранится здесь, а не в группах
}

type PermitKind = "confinedSpace" | "gasHazard" | "electrical";

// Допуск инженера. Просроченный не удаляется: validUntil < сегодня — человек не проходит подбор звена.
interface EngineerPermitDto {
  id: string;
  userId: string;
  kind: PermitKind;
  level: number | null; // confinedSpace — группа 1..3, electrical — 2..5, gasHazard — null
  validUntil: string;   // "YYYY-MM-DD"
  documentNo: string | null;
  checkedBy: string | null;
  checkedAt: string | null;
}

interface EngineerProfileDto {
  userId: string;
  brigadeId: string | null;
  phone: string | null;
  telegram: string | null;
  specialization: string[];
  status: EngineerStatus;
}

// ---- Админ-настройки модели ----

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

type WorkSource = "organizer" | "chiefDispatcher" | "carriedOver";

// График плановых работ: на окне работы модель глушит тревоги перечисленных типов на объекте
// и всех вложенных объектах. Правка — новая версия, удаление — версия с deleted; GET отдаёт только
// последнюю неудалённую версию каждой работы.
interface WorkScheduleEntryDto {
  workId: number; // целое, общее с моделью: muted_work_id прогноза, works_2026.csv
  version: number;
  objectId: number | null; // объект любого уровня; null — объект ещё не выбран
  workKind: string;
  incidentTypes: string[]; // какие тревоги гасить, пусто — не гасить
  removedSensor: string | null;
  startsAt: string;
  endsAt: string;
  source: WorkSource;
  comment: string | null;
  createdBy: string | null;
  createdAt: string;
}
```

## 2. Справочник эндпоинтов

Путь без `/api/bff` — добавляй его сам. Колонка **Право** — что нужно иметь, чтобы получить `200` вместо
`403 permission_denied` (не влияет на аутентификацию — она нужна всегда).

### Объекты

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

### Датчики

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

### Показания датчиков (окно «Логи»)

| Метод | Путь | Право | Ответ |
| --- | --- | --- | --- |
| GET | `/readings/scope?objectId=&objectId=` | любой вошедший, решает сервис | `ReadingsScopeDto` |

- `readings:read` — любой объект с потомками; без `objectId` ответ `all: true` и пустые списки.
- Без права видны объекты заявок, где пользователь назначен и работа не закрыта
  (`Assigned`, `EngineerWorking`, `ReturnedToWork`), вместе с потомками. Отчёт сдан — объект пропадает.
- До 100 `objectId`. Несуществующие отбрасываются.

### Прогнозы

| Метод | Путь | Право | Ответ |
| --- | --- | --- | --- |
| GET | `/predictions?objectId=&status=&page=&pageSize=` | `predictions:read` | `PagedResult<PredictionListItemDto>` |
| GET | `/predictions/{id}` | `predictions:read` | `PredictionDto` (с `factors`/`evidence`) |
| POST | `/predictions` | `predictions:create` | `201` + `PredictionDto`. Дедуп по `(objectId, type, hourEnd, modelVersionId)` — повтор вернёт уже существующий, не создаст дубль |
| POST | `/predictions/{id}/decisions` | `predictions:update` | `200` + `PredictionDecisionDto`. Меняет `status` прогноза (`take`→`taken`, `reject`→`rejected`, `mute`→`muted`, `reopen`→`inReview`). `take`/`reject`/`mute` — только из `new`/`inReview`, иначе `409 prediction_already_decided`; `reopen` — только из `taken`/`rejected`/`muted`, иначе `409 invalid_status`. `take` сам заводит заявку (`inWork`, диспетчер — вы) и возвращает её id в `taskId`. Решение уходит в модель (`tf.model.commands`, ML/INTEGRATION.md §13.3) |
| GET | `/fact-alerts?objectId=&page=&pageSize=` | `predictions:read` | `PagedResult<FactAlertDto>` |
| POST | `/fact-alerts` | `predictions:create` | `201` + `FactAlertDto` |

### Заявки и работы

| Метод | Путь | Право | Ответ |
| --- | --- | --- | --- |
| GET | `/tasks?dispatcherId=&status=&page=&pageSize=` | `tasks:read` | `PagedResult<WorkTaskListItemDto>` |
| GET | `/tasks/{id}` | `tasks:read` | `WorkTaskDto` (с вложенными `predictions`/`assignments`/`reports`/`returns`) |
| POST | `/tasks` | `tasks:create` | `201` + `WorkTaskDto`. Дубль `number` → `409 duplicate_code` |
| PUT | `/tasks/{id}` | `tasks:update` | `WorkTaskDto` |
| POST | `/tasks/{id}/take` | `tasks:update` | «кто первый взял — тот ведёт»: `200` + `WorkTaskDto`, либо `409 task_already_taken`, если кто-то успел раньше — **обязательно обработай этот код отдельно от прочих 409** (не ошибка данных, а гонка) |
| POST | `/tasks/{id}/predictions` | `tasks:update` | тело `{ predictionId, isPrimary }` — прикрепить прогноз как основание, `201` |
| DELETE | `/tasks/{id}/predictions/{predictionId}` | `tasks:update` | открепить (запись остаётся в истории с `detachedAt`) — `204` |
| POST | `/tasks/{id}/assignments` | `tasks:update` | тело `{ engineerId, comment? }` — `201` + `TaskAssignmentDto`, заявка переходит в `assigned`. Из `new`/`inWork`/`assigned`/`engineerWorking`/`returnedToWork`; прежнее назначение получает статус `replaced` |
| POST | `/tasks/{id}/start` | `tasks:update` | тело `{ comment? }` (можно пустое) — инженер приступил: `assigned`/`returnedToWork` → `engineerWorking`, `200` + `WorkTaskDto` |
| POST | `/tasks/{id}/reports` | `tasks:update` | тело `CreateTaskReportRequest`-подобное (`actualState?`, `worksDone?`, `resultCode`, `comment?`) — `201` + `TaskReportDto`, заявка переходит в `completed`. Только при назначенном инженере, из `assigned`/`engineerWorking`/`returnedToWork` |
| POST | `/tasks/{id}/close` | `tasks:update` | тело `{ comment? }` — отчёт принят: `completed` → `closed`, прикреплённые прогнозы → `closed`. `200` + `WorkTaskDto` |
| POST | `/tasks/{id}/cancel` | `tasks:update` | тело `{ comment? }` — отмена из любого активного статуса → `cancelled`, прикреплённые прогнозы → `closed`. `200` + `WorkTaskDto` |
| POST | `/tasks/{id}/returns` | `tasks:update` | тело `{ targetType, targetUserId?, comment? }` — `201` + `TaskReturnDto`. `queue` → заявка снова `new` без диспетчера (её можно взять); `dispatcher` → `returnedToWork` у `targetUserId` (обязателен); `incident` → `returnedToWork` у того же диспетчера. Из активных статусов и `completed` |

Переход из неподходящего статуса — `409 invalid_status`.

### Происшествия

| Метод | Путь | Право | Ответ |
| --- | --- | --- | --- |
| GET | `/incidents?objectId=&page=&pageSize=` | `incidents:read` | `PagedResult<IncidentDto>` |
| GET | `/incidents/{id}` | `incidents:read` | `IncidentDto` |
| POST | `/incidents` | `incidents:create` | `201` + `IncidentDto` |
| POST | `/incidents/{id}/confirm` | `incidents:update` | тело `{ outcome? }` — `200` + `IncidentDto` с `confirmedAt`/`confirmedBy` |

### График, закреплённые объекты, присутствие, инженеры (все — под `/users/{id}/...`, кроме `/presence` и `/brigades`)

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
| GET | `/users/{id}/permits` | `engineers:read` | `EngineerPermitDto[]`, включая просроченные |
| POST | `/users/{id}/permits` | `engineers:update` | тело `{ kind, level?, validUntil, documentNo?, checkedAt? }` — `201` + `EngineerPermitDto`. `checkedBy` — текущий пользователь, `checkedAt` по умолчанию сегодня |
| DELETE | `/users/{id}/permits/{permitId}` | `engineers:update` | `204` |
| GET | `/presence?userIds=id1&userIds=id2` | `presence:read` | `PresenceDto[]`. Без `userIds` — все. Только чтение: пишет присутствие сам BFF на каждый аутентифицированный запрос, отдельной ручки записи нет |
| GET | `/brigades` | `engineers:read` | `BrigadeDto[]` |
| POST | `/brigades` | `engineers:create` | тело `{ name, unit?, leaderId? }` — `201` + `BrigadeDto` |

### Админ-настройки модели

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
| GET | `/work-schedule?from=&to=&objectId=` | `model_settings:read` | `WorkScheduleEntryDto[]` — последние неудалённые версии, пересекающие окно `[from, to)` |
| POST | `/work-schedule` | `model_settings:manage` | тело `{ objectId?, workKind, incidentTypes?, removedSensor?, startsAt, endsAt, source, comment? }` — `201` + версия 1 |
| PUT | `/work-schedule/{workId}` | `model_settings:manage` | то же тело — новая версия, `WorkScheduleEntryDto`. Удалённая или несуществующая работа — `404` |
| DELETE | `/work-schedule/{workId}` | `model_settings:manage` | версия с `deleted`, история остаётся — `204` |

## 3. На что обратить внимание

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
- **Показаний датчиков в BFF нет.** История и живой поток — у tf-funnel:
  - `GET /api/funnel/log?objectId=` — история;
  - `GET /api/funnel/stream?objectId=` — WebSocket.

  Вход по той же cookie `access_token`. Кому что видно, воронка спрашивает у `/readings/scope`.
  Закрытие WebSocket с кодом 4401 — токен истёк: сделай любой запрос к BFF (он обновит cookie) и
  подключись снова.
