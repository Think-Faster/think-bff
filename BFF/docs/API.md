# BFF API

Пути в этом файле — **внутренние**, то есть то, что видит сам сервис (`[Route]` в контроллерах) и что
нужно указывать в Swagger/Postman при обращении к `tf-bff` напрямую внутри docker-сети.

Снаружи, через nginx (`greefob.ru`), у сервиса нет префикса `/api` — nginx сам добавляет и снимает его:
location `/api/bff/` проксирует на `tf-bff:8080/` **с обрезкой префикса** `/api/bff/`. То есть внешний
`GET https://greefob.ru/api/bff/users` превращается в `GET http://tf-bff:8080/users` — именно поэтому
контроллеры не объявляют `api/` в своих маршрутах: этот префикс — целиком забота nginx, а не BFF.

| Уровень | Пример пути |
| --- | --- |
| Снаружи (через nginx) | `https://greefob.ru/api/bff/users/{id}` |
| Внутри (то, что видит BFF, и что документировано ниже) | `/users/{id}` |

Формат ошибок везде одинаковый: `{ "code": "...", "message": "...", "details": {...} }`.

Аутентификация — `Authorization: Bearer <token>` либо кука `access_token` (имя настраивается через
`AUTH_ACCESS_TOKEN_COOKIE`). Разбирает токен `TokenAuthenticationMiddleware` — ни один контроллер не
читает заголовки сам.

В таблицах колонка **Право** — это `[RequirePermission(resource, permission)]` на экшене; «только auth» —
эндпоинту достаточно валидного токена, без проверки конкретного права.

## Health

| Метод | Путь | Право | Описание |
| --- | --- | --- | --- |
| GET | `/health/live` | нет (публично) | Процесс жив. БД не трогает. |
| GET | `/health/ready` | нет (публично) | 200, если БД и JWKS доступны, иначе 503. |

## Пользователи — `/users`

| Метод | Путь | Право |
| --- | --- | --- |
| GET | `/users?search=&page=1&pageSize=50` | `users:read` |
| GET | `/users/{id}` | `users:read` |
| POST | `/users` | `users:create` |
| PUT | `/users/{id}` | `users:update` |
| DELETE | `/users/{id}?soft=true` | `users:delete` |
| POST | `/users/{id}/groups` | `users:update` |
| DELETE | `/users/{id}/groups/{groupId}` | `users:update` |

**GET `/users/{id}`** — данные пользователя плюс группы **первого уровня** (без учёта вложенности):

```json
{
  "id": "...", "authUserId": "...",
  "lastName": "Иванов", "firstName": "Иван", "middleName": "Иванович",
  "isActive": true,
  "groups": [ { "id": "...", "code": "analysts", "name": "Аналитики" } ]
}
```

**POST `/users`** — тело `{ authUserId, lastName, firstName, middleName?, groupIds?: [] }` → `201` + объект.

**PUT `/users/{id}`** — тело `{ lastName, firstName, middleName?, isActive }`. Состав групп не меняет.

**DELETE `/users/{id}`** — `?soft=true` (по умолчанию) деактивирует (`is_active = false`); `?soft=false`
удаляет пользователя целиком вместе с его членствами в группах и его грантами.

**POST `/users/{id}/groups`** — добавление (не замена) в группы: `{ "groupIds": ["...", "..."] }`.
Несуществующая группа → `404`. Уже существующее членство — пропускается молча. Ответ — актуальный список
групп первого уровня.

**DELETE `/users/{id}/groups/{groupId}`** — исключить из группы.

## Группы — `/groups`

| Метод | Путь | Право |
| --- | --- | --- |
| GET | `/groups?search=&page=&pageSize=` | `groups:read` |
| GET | `/groups/{id}` | `groups:read` |
| POST | `/groups` | `groups:create` |
| PUT | `/groups/{id}` | `groups:update` |
| DELETE | `/groups/{id}` | `groups:delete` |
| POST | `/groups/{id}/members` | `groups:update` |
| POST | `/groups/{id}/members/batch` | `groups:update` |
| DELETE | `/groups/{id}/members/{memberType}/{memberId}` | `groups:update` |

**GET `/groups/{id}`** — данные группы плюс участники **первого уровня**, пользователи и вложенные
группы вперемешку:

```json
{
  "id": "...", "code": "analysts", "name": "Аналитики", "isSystem": false,
  "members": [
    { "type": "user",  "id": "...", "displayName": "Иванов Иван Иванович" },
    { "type": "group", "id": "...", "displayName": "Старшие аналитики", "code": "senior-analysts" }
  ]
}
```

**POST `/groups`** — `{ code, name }`. `code` уникален, дубликат → `409 duplicate_code`.

**PUT `/groups/{id}`** — `{ name }`. `code` через этот эндпоинт не меняется вообще (см. `docs/DECISIONS.md`).

**DELETE `/groups/{id}`** — запрещено для системных групп (`is_system = true`) → `409
system_group_protected`. Иначе удаляет группу, все её рёбра в графе (и как контейнер, и как участник),
её гранты, и пересчитывает замыкание графа групп.

**POST `/groups/{id}/members`** — добавить одного участника: `{ "memberId": "...", "memberType": "user"
| "group" }`. Если `memberType = group` и это создаёт цикл во вложенности → `409 cycle_detected`.
Повторное добавление — пропускается молча.

**POST `/groups/{id}/members/batch`** — `{ "members": [{ memberId, memberType }, ...] }`, вся пачка в
одной транзакции с одним пересчётом замыкания; любой цикл в пачке откатывает её целиком.

**DELETE `/groups/{id}/members/{memberType}/{memberId}`** — `memberType` — `user` или `group`.

## Права — `/permissions`, `/resources`

| Метод | Путь | Право |
| --- | --- | --- |
| GET | `/permissions/me` | только auth |
| GET | `/permissions/check?resource=&permission=` | только auth |
| GET | `/permissions/grants?principalType=&principalId=` | `permissions:read` |
| POST | `/permissions/grants` | `permissions:manage` |
| DELETE | `/permissions/grants/{id}` | `permissions:manage` |
| GET | `/resources` | только auth |
| POST | `/resources` | `permissions:manage` |

**GET `/permissions/me`** — эффективные права текущего пользователя (личные + права всех групп, в
которых он состоит, прямо или транзитивно):

```json
{
  "userId": "...",
  "permissions": {
    "users": ["read"],
    "documents": ["create", "read", "update", "export"]
  }
}
```

**GET `/permissions/check?resource=documents&permission=update`** — точечная проверка для UI (показать/
скрыть элемент): `{ "allowed": true }`.

**GET `/permissions/grants`** — выданные права принципала, опционально фильтр по `principalType`
(`user`|`group`) и `principalId`.

**POST `/permissions/grants`** — выдать/переопределить права одним upsert'ом (маска перезаписывается
целиком, не складывается с предыдущей):

```json
{ "principalType": "group", "principalId": "...", "resourceCode": "documents", "permissions": ["read", "export"] }
```

**DELETE `/permissions/grants/{id}`** — отозвать грант целиком.

**GET `/resources`** — справочник зарегистрированных ресурсов (`users`, `groups`, `permissions` и
что добавлено сверх них).

**POST `/resources`** — зарегистрировать новый код ресурса: `{ code, name }`. Дубликат кода → `409
duplicate_code`.

## Доступные значения `permission`

`create`, `read`, `update`, `delete`, `export`, `import`, `manage`. `manage` — надправо: даёт доступ к
любой операции над ресурсом, включая выдачу прав другим на этот же ресурс.

## Коды ошибок

| Код | HTTP | Где встречается |
| --- | --- | --- |
| `unauthenticated` | 401 | нет токена ни в заголовке, ни в куке |
| `invalid_token` | 401 | токен повреждён / неверная подпись / нет нужного claim'а |
| `token_refresh_failed` | 401 | токен истёк, и перевыпуск через auth-сервис не удался |
| `auth_service_unavailable` | 503 | не удалось получить/разобрать ключ подписи с `AUTH_JWKS_URL` — токены сейчас в принципе нельзя провалидировать, это не вина вызывающего |
| `user_not_provisioned` | 403 | `sub` из токена не заведён как пользователь BFF |
| `user_inactive` | 403 | пользователь деактивирован |
| `permission_denied` | 403 | не хватает права (не прошёл `[RequirePermission]`) |
| `not_found` | 404 | сущность не найдена |
| `cycle_detected` | 409 | добавление участника группы замкнуло бы цикл |
| `duplicate_code` | 409 | код группы/ресурса уже занят |
| `system_group_protected` | 409 | попытка удалить системную группу |
| `validation_failed` | 400 | тело запроса не прошло FluentValidation |
| `bad_request` | 400 | некорректный аргумент (например неизвестный `memberType`) |
| `internal_error` | 500 | необработанное исключение |
