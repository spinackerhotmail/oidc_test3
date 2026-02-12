# Локальное тестирование сервиса авторизации

## Архитектура

Сервис поддерживает несколько провайдеров авторизации.
Все провайдеры возвращают **единообразный ответ** -- JWT access token в теле
ответа, а refresh token устанавливается в **HttpOnly cookie** (недоступен
из JavaScript).

```
+-------------------+
| Swagger UI        |
| /swagger          |
+---------+---------+
          |
          v
+---------+---------+     +----------------------------+
| AuthController    |     | FakeEgiszController        |
| /api/auth/*       |---->| /realms/master/*           |
|                   |     | (mock, только Development) |
| Провайдеры:       |     +----------------------------+
|  - local          |
|  - egisz (OIDC)   |
+-------------------+
          |
          v
+---------+---------+     +-----------------------+
| JwtTokenService   |     | Redis                 |
| Выпускает единые  |     | (sessions, TTL)       |
| JWT access/refresh|     +-----------------------+
+-------------------+              ^
          |                        |
          v                        |
   Cookie: refresh_token    Сессии с автоудалением
   (HttpOnly, Secure,       по истечении срока
    SameSite=Strict)        (RefreshTokenExpiration)
```

### Хранилище данных

| Компонент | Хранилище | Назначение |
|-----------|-----------|------------|
| **Пользователи** (`AppUser`) | PostgreSQL | Долгосрочные данные: профиль, email, пароль |
| **Сессии** (`UserSession`) | Redis | Временные: refresh tokens, TTL-автоочистка |

### Провайдеры авторизации

| Провайдер | Описание | Эндпоинт логина |
|-----------|----------|-----------------|
| `local`   | Логин/пароль, пользователи из локальной БД | `POST /api/auth/login/local` |
| `egisz`   | OIDC через ИА ЕГИСЗ (mock в Development)   | `GET /api/auth/login/egisz`  |

### Единый формат ответа (для обоих провайдеров)

**Тело ответа (JSON):**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiJ9...",
  "expiresIn": 1800,
  "user": {
    "sub": "...",
    "userName": "...",
    "email": "...",
    "givenName": "...",
    "familyName": "...",
    "middleName": "...",
    "authProvider": "local"
  },
  "authProvider": "local"
}
```

**Cookie (устанавливается автоматически):**

```
Set-Cookie: refresh_token=<value>; HttpOnly; Secure; SameSite=Strict; Path=/api/auth; Max-Age=86400
```

> **Refresh token никогда не передаётся в теле ответа.**
> Он хранится в HttpOnly cookie и автоматически отправляется браузером
> при запросах к `/api/auth/refresh` и `/api/auth/logout`.

## Предварительные требования

- .NET 10 SDK
- Docker (для PostgreSQL и Redis)

## Запуск

### 1. Запустить PostgreSQL и Redis

```bash
docker-compose up -d
```

Контейнеры стартуют на портах:
- **PostgreSQL:** `54320` (только для таблицы Users)
- **Redis:** `6379` (хранилище сессий)

### 2. Запустить сервис

```bash
cd OIDC_test3
dotnet run --launch-profile https
```

### 3. Открыть Swagger UI

```
https://localhost:7237/swagger
```

Миграции БД применятся автоматически при старте.

---

## Сценарий A -- Локальная авторизация (login/password)

### A1. Регистрация пользователя

В Swagger UI раскройте **`POST /api/auth/register`**, нажмите **Try it out**,
укажите тело запроса:

```json
{
  "username": "testuser",
  "password": "MyPassword123",
  "email": "test@example.com",
  "givenName": "Тест",
  "familyName": "Тестов",
  "middleName": "Тестович"
}
```

Нажмите **Execute**. В ответе -- данные созданного пользователя.

### A2. Логин

Раскройте **`POST /api/auth/login/local`**, нажмите **Try it out**:

```json
{
  "username": "testuser",
  "password": "MyPassword123"
}
```

Нажмите **Execute**. В ответе -- JWT access token:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiJ9...",
  "expiresIn": 1800,
  "user": { "sub": "local:...", "userName": "testuser", ... },
  "authProvider": "local"
}
```

Одновременно в браузер установится HttpOnly cookie `refresh_token`.

**Скопируйте значение `accessToken`** из ответа.

### A3. Авторизация в Swagger UI

Нажмите кнопку **Authorize** (замок) в правом верхнем углу Swagger UI.
В появившемся окне вставьте скопированный `accessToken` в поле **Value** (без префикса "Bearer").
Нажмите **Authorize**, затем **Close**.

Теперь все запросы из Swagger UI будут автоматически отправлять заголовок `Authorization: Bearer <token>`.

### A4. Получение текущего пользователя

Раскройте **`GET /api/auth/me`** -> **Try it out** -> **Execute**.

Ответ содержит данные авторизованного пользователя из JWT-токена.

### A5. Обновление токена

Раскройте **`POST /api/auth/refresh`** -> **Try it out** -> **Execute**.

Тело запроса не требуется -- refresh token читается из HttpOnly cookie
автоматически.

В ответе -- новый `accessToken`:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiJ9...",
  "expiresIn": 1800
}
```

Cookie `refresh_token` также обновляется автоматически.

> Обновите токен в кнопке **Authorize** в Swagger UI, если требуется выполнить новые запросы к защищённым эндпоинтам.

### A6. Выход

Раскройте **`POST /api/auth/logout`** -> **Try it out** -> **Execute**.

Тело запроса не требуется -- refresh token читается из cookie.
Все сессии пользователя инвалидированы, cookie `refresh_token` удалена.

---

## Сценарий B -- Авторизация через ЕГИСЗ (OIDC)

### B1. Логин

> Этот эндпоинт выполняет redirect, поэтому его нужно открыть **в браузере**.

Откройте в адресной строке:

```
https://localhost:7237/api/auth/login/egisz
```

В Development-режиме произойдёт автоматический цикл:
redirect -> FakeEgiszController (mock) -> callback -> JSON-ответ.

Ответ -- тот же формат, что и для локальной авторизации:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiJ9...",
  "expiresIn": 1800,
  "user": {
    "sub": "acd969f9-7fe1-4230-b572-aa2c0d9b7009",
    "userName": "ivanov",
    "email": "ivanov@example.com",
    "givenName": "...",
    "familyName": "...",
    "middleName": "...",
    "authProvider": "egisz"
  },
  "authProvider": "egisz"
}
```

Cookie `refresh_token` устанавливается автоматически.

**Скопируйте `accessToken`** из JSON-ответа в браузере.

### B2. Авторизация в Swagger UI

Нажмите кнопку **Authorize** в Swagger UI -> вставьте `accessToken` -> **Authorize** -> **Close**.

Далее работайте с эндпоинтами `GET /api/auth/me`, `POST /api/auth/refresh`,
`POST /api/auth/logout` точно так же, как в сценарии A.

---

## Управление fake-пользователями ЕГИСЗ (через Swagger UI)

Эндпоинты mock-сервера доступны в секции **FakeEgisz** в Swagger UI.

### Список fake-пользователей

**`GET /realms/master/fake/users`** -> **Try it out** -> **Execute**.

### Переключение пользователя

**`POST /realms/master/fake/switch-user`** -> **Try it out** -> **Execute**.

После переключения следующий вызов `/api/auth/login/egisz` авторизует другого пользователя.

### Доступные fake-пользователи ЕГИСЗ

| Username | Email                | Имя  | Фамилия | Отчество   |
|----------|----------------------|------|---------|------------|
| ivanov   | ivanov@example.com   | Иван | Иванов  | Петрович   |
| petrov   | petrov@example.com   | Пётр | Петров  | Сергеевич  |

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

## Полные сценарии тестирования

### Локальная авторизация

```
 1. docker-compose up -d
 2. dotnet run --launch-profile https
 3. Открыть https://localhost:7237/swagger
 4. POST /api/auth/register          -> создать пользователя
 5. POST /api/auth/login/local       -> получить accessToken (+ cookie)
 6. Authorize в Swagger UI           -> вставить accessToken
 7. GET  /api/auth/me                -> проверить текущего пользователя
 8. POST /api/auth/refresh           -> обновить токены (без тела)
 9. POST /api/auth/logout            -> выход (без тела)
10. GET  /api/auth/me                -> убедиться что 401
```

### Авторизация через ЕГИСЗ

```
 1. docker-compose up -d
 2. dotnet run --launch-profile https
 3. Открыть https://localhost:7237/swagger
 4. В браузере: /api/auth/login/egisz -> получить accessToken (+ cookie)
 5. Authorize в Swagger UI            -> вставить accessToken
 6. GET  /api/auth/me                 -> проверить текущего пользователя
 7. POST /api/auth/refresh            -> обновить токены (без тела)
 8. POST /api/auth/logout             -> выход (без тела)
 9. GET  /api/auth/me                 -> убедиться что 401
```

---

## Переход на реальную ИА ЕГИСЗ

Замените в `appsettings.json`:

```json
"EgiszOidc": {
  "Authority": "https://ia-test.egisz.rosminzdrav.ru/realms/master",
  "ClientId": "ваш-реальный-client-id",
  "ClientSecret": "ваш-реальный-client-secret"
}
```

`FakeEgiszController` не используется за пределами Development --
Authority будет указывать на реальный сервер ИА ЕГИСЗ.
