# Руководство по интеграции для внешних сервисов

## Обзор

Сервис авторизации ЕГИСЗ предоставляет единую точку аутентификации пользователей
через ИА ЕГИСЗ (OpenID Connect) или локальный логин/пароль для всех подключённых внешних сервисов.

Внешний сервис **не взаимодействует напрямую с ИА ЕГИСЗ** -- вместо этого
он работает через API данного сервиса авторизации.

```
+------------------+     +----------------------------+     +---------------+
|  Внешний сервис   | --> |  Сервис авторизации ЕГИСЗ  | --> |  ИА ЕГИСЗ     |
|  (ваш backend)   | <-- |  (этот API)                | <-- |  (OIDC IdP)   |
+------------------+     +----------------------------+     +---------------+
                              |                                      
                              v                                      
                         Redis (сессии)
                         PostgreSQL (users)
```

## Базовый URL

```
https://<auth-service-host>
```

## Архитектура безопасности

### Два типа токенов

| Токен | Где хранится | Срок жизни | Назначение |
|-------|--------------|------------|------------|
| **Access Token** (JWT) | Тело ответа → localStorage/память клиента | 30 мин (по умолчанию) | Авторизация API-запросов (Bearer) |
| **Refresh Token** | HttpOnly Cookie (недоступен из JS) | 24 часа (по умолчанию) | Обновление access token |

### Почему HttpOnly Cookie?

✅ **Защита от XSS** — JavaScript не может прочитать refresh token  
✅ **Автоматическая отправка** — браузер сам отправляет cookie при запросах к `/api/auth/*`  
✅ **Безопасность** — украденный access token действует только 30 мин

---

## Сценарии интеграции

### Сценарий 1 -- Аутентификация пользователя через ЕГИСЗ (OIDC)

Используется когда внешнему сервису нужно идентифицировать пользователя через ИА ЕГИСЗ.

#### Шаг 1. Перенаправить пользователя на логин

Из фронтенда перенаправьте пользователя на:

```
GET /api/auth/login/egisz?returnUrl=https://your-service.example.com/after-login
```

| Параметр    | Обязателен | Описание                                                   |
|-------------|------------|------------------------------------------------------------|
| `returnUrl` | Нет        | URL, куда нужно вернуть пользователя после входа (резерв). |

Сервис авторизации сам выполнит редирект на ИА ЕГИСЗ и обратно.

#### Шаг 2. Получить результат из callback

После успешной аутентификации сервис вернёт JSON-ответ в браузер:

**Ответ (200 OK):**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiJ9...",
  "expiresIn": 1800,
  "user": {
    "sub": "acd969f9-7fe1-4230-b572-aa2c0d9b7009",
    "userName": "ivanov",
    "email": "ivanov@example.com",
    "givenName": "Иван",
    "familyName": "Иванов",
    "middleName": "Петрович",
    "authProvider": "egisz"
  },
  "authProvider": "egisz"
}
```

**Одновременно устанавливается cookie:**

```
Set-Cookie: refresh_token=<value>; HttpOnly; Secure; SameSite=Strict; Path=/api/auth; Max-Age=86400
```

#### Шаг 3. Сохранить accessToken на фронтенде

Ваш фронтенд должен:

1. **Извлечь `accessToken`** из JSON-ответа
2. **Сохранить** в памяти или `sessionStorage` (не в `localStorage` для безопасности)
3. **Использовать** для всех API-запросов в заголовке `Authorization: Bearer <accessToken>`

**Пример (JavaScript):**

```javascript
// После redirect на /callback/egisz браузер отображает JSON
const response = await fetch(window.location.href);
const data = await response.json();

// Сохранить accessToken
sessionStorage.setItem('accessToken', data.accessToken);
sessionStorage.setItem('user', JSON.stringify(data.user));

// Перенаправить на главную страницу вашего приложения
window.location.href = '/dashboard';
```

---

### Сценарий 2 -- Локальная аутентификация (логин/пароль)

Для тестирования или внутренних пользователей без доступа к ИА ЕГИСЗ.

#### Шаг 1. Зарегистрировать пользователя

```http
POST /api/auth/register
Content-Type: application/json

{
  "username": "testuser",
  "password": "MyPassword123",
  "email": "test@example.com",
  "givenName": "Тест",
  "familyName": "Тестов",
  "middleName": "Тестович"
}
```

**Ответ (200 OK):**

```json
{
  "sub": "local:550e8400-e29b-41d4-a716-446655440000",
  "userName": "testuser",
  "email": "test@example.com",
  "givenName": "Тест",
  "familyName": "Тестов",
  "middleName": "Тестович",
  "authProvider": "local"
}
```

#### Шаг 2. Выполнить логин

```http
POST /api/auth/login/local
Content-Type: application/json

{
  "username": "testuser",
  "password": "MyPassword123"
}
```

**Ответ (200 OK):**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiJ9...",
  "expiresIn": 1800,
  "user": {
    "sub": "local:550e8400-...",
    "userName": "testuser",
    "email": "test@example.com",
    "givenName": "Тест",
    "familyName": "Тестов",
    "middleName": "Тестович",
    "authProvider": "local"
  },
  "authProvider": "local"
}
```

+ Cookie `refresh_token` (HttpOnly)

---

### Сценарий 3 -- Использование защищённых эндпоинтов

Все запросы к защищённым эндпоинтам требуют `Authorization: Bearer <accessToken>`.

#### Пример: Получить данные текущего пользователя

```http
GET /api/auth/me
Authorization: Bearer eyJhbGciOiJIUzI1NiJ9...
```

**Ответ (200 OK):**

```json
{
  "sub": "acd969f9-7fe1-4230-b572-aa2c0d9b7009",
  "userName": "ivanov",
  "email": "ivanov@example.com",
  "givenName": "Иван",
  "familyName": "Иванов",
  "middleName": "Петрович",
  "authProvider": "egisz"
}
```

**Ответ (401 Unauthorized):** Если токен истёк или невалиден

```json
{
  "error": "Unauthorized"
}
```

---

### Сценарий 4 -- Обновление токена (Refresh)

Когда `accessToken` истекает (через 30 минут), обновите его:

```http
POST /api/auth/refresh
Cookie: refresh_token=<value>
```

> **Важно:** `refresh_token` отправляется автоматически браузером из HttpOnly cookie.
> Тело запроса пустое.

**Ответ (200 OK):**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiJ9...",
  "expiresIn": 1800
}
```

**Ответ (401 Unauthorized):** Если refresh token истёк или невалиден

```json
{
  "error": "Invalid or expired refresh token."
}
```

> После успешного refresh **старая сессия инвалидируется**, выдаётся новый `refresh_token` (cookie обновляется автоматически).

---

### Сценарий 5 -- Выход пользователя (Logout)

```http
POST /api/auth/logout
Cookie: refresh_token=<value>
```

**Ответ (200 OK):**

```json
{
  "message": "Logged out successfully."
}
```

**Что происходит:**

1. Все сессии пользователя инвалидируются (logout everywhere)
2. Cookie `refresh_token` удаляется
3. Если пользователь авторизован через ЕГИСЗ, отправляется запрос Single Logout в ИА ЕГИСЗ

---

## Справочник API

| Метод  | Эндпоинт                       | Auth   | Описание                                      |
|--------|--------------------------------|--------|-----------------------------------------------|
| `GET`  | `/api/auth/providers`          | --     | Список доступных провайдеров                  |
| `POST` | `/api/auth/register`           | --     | Регистрация локального пользователя            |
| `POST` | `/api/auth/login/local`        | --     | Логин (пароль). Cookie: `refresh_token`        |
| `GET`  | `/api/auth/login/egisz`        | --     | Начать OIDC-поток (redirect)                   |
| `GET`  | `/api/auth/callback/egisz`     | --     | OIDC callback. Cookie: `refresh_token`         |
| `POST` | `/api/auth/refresh`            | Cookie | Обновить токены (из cookie `refresh_token`)    |
| `GET`  | `/api/auth/me`                 | Bearer | Данные текущего пользователя                   |
| `POST` | `/api/auth/logout`             | Cookie | Выход (из cookie `refresh_token`)              |

---

## Примеры интеграции

### JavaScript / TypeScript (SPA)

```javascript
const AUTH_BASE = 'https://auth-service.example.com';

// 1. Перенаправить на логин ЕГИСЗ
function loginEgisz(returnUrl) {
  window.location.href =
    `${AUTH_BASE}/api/auth/login/egisz?returnUrl=${encodeURIComponent(returnUrl)}`;
}

// 2. После callback получить accessToken из JSON-ответа браузера
// (этот код выполняется на странице /callback/egisz)
async function handleCallback() {
  const response = await fetch(window.location.href);
  const data = await response.json();
  
  // Сохранить в sessionStorage
  sessionStorage.setItem('accessToken', data.accessToken);
  sessionStorage.setItem('user', JSON.stringify(data.user));
  
  // Перенаправить на главную
  window.location.href = '/dashboard';
}

// 3. Получить данные текущего пользователя
async function getCurrentUser() {
  const token = sessionStorage.getItem('accessToken');
  
  const res = await fetch(`${AUTH_BASE}/api/auth/me`, {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  
  if (!res.ok) {
    // Токен истёк, попробовать refresh
    await refreshToken();
    return getCurrentUser(); // повторить запрос
  }
  
  return await res.json();
}

// 4. Обновить токен (refresh_token из cookie автоматически)
async function refreshToken() {
  const res = await fetch(`${AUTH_BASE}/api/auth/refresh`, {
    method: 'POST',
    credentials: 'include' // важно! отправляет cookie
  });
  
  if (!res.ok) {
    // Refresh token истёк, нужен новый login
    window.location.href = `${AUTH_BASE}/api/auth/login/egisz`;
    return;
  }
  
  const data = await res.json();
  sessionStorage.setItem('accessToken', data.accessToken);
}

// 5. Выход (refresh_token из cookie автоматически)
async function logout() {
  await fetch(`${AUTH_BASE}/api/auth/logout`, {
    method: 'POST',
    credentials: 'include' // важно! отправляет cookie
  });
  
  sessionStorage.clear();
  window.location.href = '/';
}

// 6. Защищённый API-запрос к вашему бэкенду
async function callProtectedApi(endpoint) {
  const token = sessionStorage.getItem('accessToken');
  
  const res = await fetch(`https://your-backend.com${endpoint}`, {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  
  if (res.status === 401) {
    // Токен истёк
    await refreshToken();
    return callProtectedApi(endpoint); // повторить
  }
  
  return await res.json();
}
```

---

### C# (Backend-to-Backend)

Если ваш backend должен вызывать API сервиса авторизации от имени пользователя:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

public class AuthServiceClient
{
    private readonly HttpClient _http;
    private readonly string _authBaseUrl;

    public AuthServiceClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _authBaseUrl = config["AuthService:BaseUrl"]!.TrimEnd('/');
        
        // Важно: HttpClient должен поддерживать cookies
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new System.Net.CookieContainer()
        };
        _http = new HttpClient(handler) { BaseAddress = new Uri(_authBaseUrl) };
    }

    // Локальный логин (для тестирования)
    public async Task<LoginResponse?> LoginLocalAsync(string username, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login/local", new
        {
            username,
            password
        });
        
        if (!response.IsSuccessStatusCode)
            return null;
        
        return await response.Content.ReadFromJsonAsync<LoginResponse>();
    }

    // Получить данные пользователя (требует Bearer token)
    public async Task<UserInfo?> GetCurrentUserAsync(string accessToken)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        
        var response = await _http.GetAsync("/api/auth/me");
        
        if (!response.IsSuccessStatusCode)
            return null;
        
        return await response.Content.ReadFromJsonAsync<UserInfo>();
    }

    // Обновить токен (refresh_token из cookie автоматически)
    public async Task<RefreshResponse?> RefreshAsync()
    {
        var response = await _http.PostAsync("/api/auth/refresh", null);
        
        if (!response.IsSuccessStatusCode)
            return null;
        
        return await response.Content.ReadFromJsonAsync<RefreshResponse>();
    }

    // Выход (refresh_token из cookie автоматически)
    public async Task LogoutAsync()
    {
        await _http.PostAsync("/api/auth/logout", null);
    }
}

public record LoginResponse(
    string AccessToken,
    int ExpiresIn,
    UserInfo User,
    string AuthProvider);

public record UserInfo(
    string Sub,
    string? UserName,
    string? Email,
    string? GivenName,
    string? FamilyName,
    string? MiddleName,
    string? AuthProvider);

public record RefreshResponse(
    string AccessToken,
    int ExpiresIn);
```

---

## Типичный поток для web-приложения (SPA)

```
Пользователь          Фронтенд (SPA)        Сервис авторизации       ИА ЕГИСЗ
     |                    |                        |                     |
     |  Нажимает "Войти"  |                        |                     |
     |------------------->|                        |                     |
     |                    |  redirect /login/egisz |                     |
     |                    |----------------------->|                     |
     |                    |                        |  redirect /auth     |
     |<----------------------------------------------------- ---------->|
     |                    |                        |                     |
     |  Вводит логин/пароль в ИА ЕГИСЗ             |                     |
     |--------------------------------------------------------------- ->|
     |                    |                        |  redirect /callback |
     |                    |                        |<--------------------|
     |                    |  200 OK + JSON         |                     |
     |                    |  + Cookie refresh_token|                     |
     |                    |<-----------------------|                     |
     |                    |                        |                     |
     |                    |  Сохраняет accessToken |                     |
     |                    |  в sessionStorage      |                     |
     |  Авторизован       |                        |                     |
     |<-------------------|                        |                     |
     |                    |                        |                     |
     |  Запрос к API      |                        |                     |
     |------------------->|                        |                     |
     |                    |  GET /me               |                     |
     |                    |  Authorization: Bearer |                     |
     |                    |----------------------->|                     |
     |                    |  200 OK (user data)    |                     |
     |                    |<-----------------------|                     |
     |  Ответ             |                        |                     |
     |<-------------------|                        |                     |
     |                    |                        |                     |
     |  (через 30 мин)    |                        |                     |
     |  401 Unauthorized  |                        |                     |
     |<-------------------|                        |                     |
     |                    |  POST /refresh         |                     |
     |                    |  Cookie: refresh_token |                     |
     |                    |----------------------->|                     |
     |                    |  200 OK + new token    |                     |
     |                    |<-----------------------|                     |
     |                    |  Обновляет accessToken |                     |
```

---

## Обработка ошибок

| HTTP-код | Когда возникает                                 | Действие                                  |
|----------|-------------------------------------------------|-------------------------------------------|
| `200`    | Успешный запрос                                 | Обработать ответ                          |
| `400`    | Невалидные параметры                            | Проверить тело запроса                    |
| `401`    | Access token истёк или невалиден                | Вызвать `POST /api/auth/refresh`          |
| `401`    | Refresh token истёк или невалиден               | Перенаправить на `/api/auth/login/egisz`  |
| `404`    | Пользователь не найден                          | Проверить данные                          |
| `500`    | Ошибка связи с ИА ЕГИСЗ или внутренняя ошибка  | Повторить запрос / показать ошибку        |

---

## SSO -- Single Sign-On

Если пользователь уже авторизован в ИА ЕГИСЗ через другой сервис,
при вызове `/api/auth/login/egisz` он **не увидит форму логина** --
ИА ЕГИСЗ автоматически выдаст код авторизации и перенаправит обратно.

Для внешнего сервиса это прозрачно -- поток остаётся тем же.

---

## SLO -- Single Logout

При вызове `POST /api/auth/logout`:
1. Инвалидируются **все** сессии данного пользователя в Redis
2. Cookie `refresh_token` удаляется
3. Если пользователь авторизован через ЕГИСЗ, отправляется запрос на завершение глобальной сессии в ИА ЕГИСЗ
4. Пользователь выходит из **всех** подключённых подсистем ЕГИСЗ

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

### Использование кнопки Authorize

1. Выполните login через Swagger UI (`POST /api/auth/login/local`)
2. Скопируйте `accessToken` из ответа
3. Нажмите кнопку **Authorize** (замок) в правом верхнем углу
4. Вставьте токен в поле **Value** (без префикса "Bearer")
5. Нажмите **Authorize**, затем **Close**

Теперь все запросы будут автоматически отправлять `Authorization: Bearer <token>`.
