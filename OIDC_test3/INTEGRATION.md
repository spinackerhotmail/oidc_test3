# Руководство по интеграции для внешних сервисов

## Обзор

Сервис авторизации ЕГИСЗ предоставляет единую точку аутентификации пользователей
через ИА ЕГИСЗ (OpenID Connect) для всех подключённых внешних сервисов.

Внешний сервис **не взаимодействует напрямую с ИА ЕГИСЗ** -- вместо этого
он работает через API данного сервиса авторизации.

```
+------------------+     +----------------------------+     +---------------+
|  Внешний сервис   | --> |  Сервис авторизации ЕГИСЗ  | --> |  ИА ЕГИСЗ     |
|  (ваш backend)   | <-- |  (этот API)                | <-- |  (OIDC IdP)   |
+------------------+     +----------------------------+     +---------------+
```

## Базовый URL

```
https://<auth-service-host>
```

## Сценарии интеграции

---

### Сценарий 1 -- Аутентификация пользователя (Authorization Code Flow)

Используется когда внешнему сервису нужно идентифицировать пользователя.

#### Шаг 1. Перенаправить пользователя на логин

Из фронтенда или бэкенда перенаправьте пользователя на:

```
GET /api/auth/login?returnUrl=https://your-service.example.com/after-login
```

| Параметр    | Обязателен | Описание                                                   |
|-------------|------------|------------------------------------------------------------|
| `returnUrl` | Нет        | URL, куда нужно вернуть пользователя после входа (резерв). |

Сервис авторизации сам выполнит редирект на ИА ЕГИСЗ и обратно.

#### Шаг 2. Получить результат из callback

После успешной аутентификации сервис вернёт JSON-ответ:

```http
GET /api/auth/callback?code=...&state=...
```

**Ответ (200 OK):**

```json
{
  "sessionId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "accessToken": "eyJhbGciOiJSUzI1NiJ9...",
  "expiresIn": 300,
  "refreshToken": "eyJhbGciOiJSUzI1NiJ9...",
  "idToken": "eyJhbGciOiJSUzI1NiJ9...",
  "user": {
    "sub": "acd969f9-7fe1-4230-b572-aa2c0d9b7009",
    "userName": "ivanov",
    "email": "ivanov@example.com",
    "givenName": "Иван",
    "familyName": "Иванов",
    "middleName": "Петрович"
  }
}
```

> **Важно:** Сохраните `sessionId` -- это ключ для всех дальнейших операций.

#### Шаг 3. Сохранить sessionId

Сохраните `sessionId` на стороне внешнего сервиса (в cookie, в сессии, в localStorage фронтенда)
и используйте его для проверки авторизации и получения данных пользователя.

---

### Сценарий 2 -- Проверка авторизации пользователя

Перед выполнением защищённого действия внешний сервис проверяет, активна ли сессия.

```http
GET /api/auth/session/{sessionId}
```

**Ответ (200 OK):**

```json
{
  "sessionId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "isActive": true,
  "accessTokenExpiresAt": "2025-02-12T10:30:00Z",
  "user": {
    "sub": "acd969f9-7fe1-4230-b572-aa2c0d9b7009",
    "userName": "ivanov",
    "email": "ivanov@example.com",
    "givenName": "Иван",
    "familyName": "Иванов",
    "middleName": "Петрович"
  }
}
```

**Ответ (404 Not Found):**

```json
{
  "error": "Session not found or inactive."
}
```

> Если сессия не найдена или неактивна -- перенаправьте пользователя на `/api/auth/login`.

---

### Сценарий 3 -- Получение данных пользователя из ИА ЕГИСЗ

Если нужны актуальные данные из ИА ЕГИСЗ (а не из локальной БД):

```http
GET /api/auth/userinfo/{sessionId}
```

**Ответ (200 OK):**

```json
{
  "sub": "acd969f9-7fe1-4230-b572-aa2c0d9b7009",
  "userName": "ivanov",
  "email": "ivanov@example.com",
  "givenName": "Иван",
  "familyName": "Иванов",
  "middleName": "Петрович"
}
```

---

### Сценарий 4 -- Обновление токена

Когда `accessTokenExpiresAt` приближается или уже прошёл, обновите токен:

```http
POST /api/auth/refresh
Content-Type: application/json

{
  "sessionId": "f47ac10b-58cc-4372-a567-0e02b2c3d479"
}
```

**Ответ (200 OK):**

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiJ9...",
  "expiresIn": 300,
  "refreshToken": "eyJhbGciOiJSUzI1NiJ9...",
  "idToken": "eyJhbGciOiJSUzI1NiJ9..."
}
```

> **Внимание:** После refresh старая сессия инвалидируется. Новый `sessionId` нужно
> получить повторным вызовом `/api/auth/session/{sessionId}` или из следующего login.

---

### Сценарий 5 -- Выход пользователя (Single Logout)

#### Вариант A -- API-вызов (из бэкенда)

Инвалидирует все сессии пользователя и уведомляет ИА ЕГИСЗ.

```http
POST /api/auth/logout
Content-Type: application/json

{
  "sessionId": "f47ac10b-58cc-4372-a567-0e02b2c3d479"
}
```

**Ответ (200 OK):**

```json
{
  "message": "Logged out successfully."
}
```

#### Вариант B -- Redirect (из браузера)

Перенаправляет пользователя на IA ЕГИСЗ для завершения глобальной сессии:

```
GET /api/auth/logout/{sessionId}
```

Браузер будет перенаправлен на ИА ЕГИСЗ logout, затем обратно на сервис авторизации.

> **Single Logout:** при выходе из одного внешнего сервиса инвалидируются **все** сессии
> данного пользователя -- во всех подключённых сервисах.

---

## Справочник API

| Метод  | Эндпоинт                       | Описание                                       |
|--------|--------------------------------|-------------------------------------------------|
| `GET`  | `/api/auth/login`              | Начать авторизацию (redirect на ИА ЕГИСЗ)       |
| `GET`  | `/api/auth/callback`           | OIDC callback (вызывается автоматически)        |
| `GET`  | `/api/auth/session/{id}`       | Проверить сессию, получить данные пользователя   |
| `GET`  | `/api/auth/userinfo/{id}`      | Данные пользователя из ИА ЕГИСЗ                 |
| `POST` | `/api/auth/refresh`            | Обновить токены                                 |
| `POST` | `/api/auth/logout`             | Выход (API, Single Logout)                      |
| `GET`  | `/api/auth/logout/{id}`        | Выход (browser redirect, Single Logout)         |

---

## Пример интеграции

### C# (HttpClient)

```csharp
public class AuthServiceClient
{
    private readonly HttpClient _http;
    private readonly string _authBaseUrl;

    public AuthServiceClient(HttpClient http, string authBaseUrl)
    {
        _http = http;
        _authBaseUrl = authBaseUrl.TrimEnd('/');
    }

    // Получить URL для редиректа пользователя на логин
    public string GetLoginUrl(string returnUrl)
    {
        return $"{_authBaseUrl}/api/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
    }

    // Проверить сессию
    public async Task<SessionInfo?> GetSessionAsync(Guid sessionId)
    {
        var response = await _http.GetAsync($"{_authBaseUrl}/api/auth/session/{sessionId}");
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<SessionInfo>();
    }

    // Обновить токен
    public async Task<RefreshResult?> RefreshAsync(Guid sessionId)
    {
        var response = await _http.PostAsJsonAsync(
            $"{_authBaseUrl}/api/auth/refresh",
            new { sessionId });
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<RefreshResult>();
    }

    // Выход
    public async Task LogoutAsync(Guid sessionId)
    {
        await _http.PostAsJsonAsync(
            $"{_authBaseUrl}/api/auth/logout",
            new { sessionId });
    }
}

public record SessionInfo(Guid SessionId, bool IsActive, DateTime? AccessTokenExpiresAt, UserInfo User);
public record UserInfo(string Sub, string? UserName, string? Email,
    string? GivenName, string? FamilyName, string? MiddleName);
public record RefreshResult(string AccessToken, int ExpiresIn, string RefreshToken, string IdToken);
```

### JavaScript (fetch)

```javascript
const AUTH_BASE = 'https://auth-service.example.com';

// Перенаправить на логин
function login(returnUrl) {
  window.location.href =
    `${AUTH_BASE}/api/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}

// Проверить сессию
async function getSession(sessionId) {
  const res = await fetch(`${AUTH_BASE}/api/auth/session/${sessionId}`);
  if (!res.ok) return null;
  return await res.json();
}

// Обновить токен
async function refresh(sessionId) {
  const res = await fetch(`${AUTH_BASE}/api/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sessionId })
  });
  if (!res.ok) return null;
  return await res.json();
}

// Выход
async function logout(sessionId) {
  await fetch(`${AUTH_BASE}/api/auth/logout`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sessionId })
  });
}
```

---

## Типичный поток для web-приложения

```
Пользователь          Ваш сервис            Сервис авторизации       ИА ЕГИСЗ
     |                    |                        |                     |
     |  Нажимает "Войти"  |                        |                     |
     |------------------->|                        |                     |
     |                    |  redirect /api/auth/login                    |
     |                    |----------------------->|                     |
     |                    |                        |  redirect /auth     |
     |<----------------------------------------------------- ---------->|
     |                    |                        |                     |
     |  Вводит логин/пароль в ИА ЕГИСЗ             |                     |
     |--------------------------------------------------------------- ->|
     |                    |                        |  redirect /callback |
     |                    |                        |<--------------------|
     |                    |  200 OK + JSON         |                     |
     |                    |<-----------------------|                     |
     |                    |                        |                     |
     |                    |  Сохраняет sessionId   |                     |
     |  Авторизован       |                        |                     |
     |<-------------------|                        |                     |
     |                    |                        |                     |
     |  Защищённый запрос |                        |                     |
     |------------------->|                        |                     |
     |                    |  GET /session/{id}     |                     |
     |                    |----------------------->|                     |
     |                    |  200 OK (isActive:true)|                     |
     |                    |<-----------------------|                     |
     |  Ответ             |                        |                     |
     |<-------------------|                        |                     |
```

---

## Обработка ошибок

| HTTP-код | Когда возникает                                 | Действие                                  |
|----------|-------------------------------------------------|-------------------------------------------|
| `200`    | Успешный запрос                                 | Обработать ответ                          |
| `400`    | Невалидные параметры                            | Проверить тело запроса                    |
| `404`    | Сессия не найдена / неактивна                   | Перенаправить на `/api/auth/login`        |
| `500`    | Ошибка связи с ИА ЕГИСЗ или внутренняя ошибка  | Повторить запрос / показать ошибку        |

---

## SSO -- Single Sign-On

Если пользователь уже авторизован в ИА ЕГИСЗ через другой сервис,
при вызове `/api/auth/login` он **не увидит форму логина** --
ИА ЕГИСЗ автоматически выдаст код авторизации и перенаправит обратно.

Для внешнего сервиса это прозрачно -- поток остаётся тем же.

## SLO -- Single Logout

При вызове `POST /api/auth/logout` или `GET /api/auth/logout/{id}`:
1. Инвалидируются **все** сессии данного пользователя в сервисе авторизации.
2. Отправляется запрос на завершение глобальной сессии в ИА ЕГИСЗ.
3. Пользователь выходит из **всех** подключённых подсистем ЕГИСЗ.

---

## Swagger UI

Документация API с возможностью интерактивного тестирования доступна по адресу:

```
https://<auth-service-host>/swagger
```

OpenAPI-спецификация:

```
https://<auth-service-host>/openapi/v1.json
```
