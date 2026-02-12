# Локальное тестирование сервиса авторизации ЕГИСЗ

## Архитектура тестирования

В режиме `Development` сервис содержит:
- **Swagger UI** (`/swagger`) -- интерактивный интерфейс для вызова всех API-эндпоинтов
- **Mock IA ЕГИСЗ** (`FakeEgiszController`) -- имитация OIDC-эндпоинтов ИА ЕГИСЗ локально

```
Swagger UI (/swagger)
    |
    v
+----------------------------+
|  AuthController            |  <-- сервис авторизации
|  /api/auth/*               |
+----------------------------+
           | HTTP-запросы к "IA ЕГИСЗ"
           v
+----------------------------+
|  FakeEgiszController       |  <-- mock, встроен в тот же сервис
|  /realms/master/*          |
+----------------------------+
```

## Предварительные требования

- .NET 10 SDK
- Docker (для PostgreSQL)

## Запуск

### 1. Запустить PostgreSQL

```bash
docker-compose up -d
```

Контейнер стартует на порту **54320** (настройки из `appsettings.json`).

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

## Тестирование через Swagger UI

### Шаг 1 -- Авторизация (Authorization Code Flow)

> Этот эндпоинт выполняет редирект, поэтому его нужно открыть **в браузере**, а не через Swagger.

Откройте в адресной строке браузера:

```
https://localhost:7237/api/auth/login
```

Произойдёт автоматический цикл: redirect -> fake IA -> callback.
В ответе вы получите JSON:

```json
{
  "sessionId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "accessToken": "fake-access-...",
  "expiresIn": 300,
  "refreshToken": "fake-refresh-...",
  "idToken": "fake-id-...",
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

**Скопируйте значение `sessionId`** -- оно понадобится для всех дальнейших запросов.

### Шаг 2 -- Проверка сессии

В Swagger UI раскройте **`GET /api/auth/session/{sessionId}`**, нажмите **Try it out**,
вставьте `sessionId` из шага 1 и нажмите **Execute**.

### Шаг 3 -- Получение UserInfo

Раскройте **`GET /api/auth/userinfo/{sessionId}`**, нажмите **Try it out**,
вставьте `sessionId` и нажмите **Execute**.
Ответ содержит данные пользователя, полученные из mock IA ЕГИСЗ.

### Шаг 4 -- Обновление токена (Refresh)

Раскройте **`POST /api/auth/refresh`**, нажмите **Try it out**,
в теле запроса укажите:

```json
{
  "sessionId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

Нажмите **Execute**. В ответе будут новые токены.

### Шаг 5 -- Logout (Single Logout)

Раскройте **`POST /api/auth/logout`**, нажмите **Try it out**,
в теле запроса укажите:

```json
{
  "sessionId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

Нажмите **Execute**. Все сессии пользователя будут инвалидированы,
и mock IA получит уведомление о выходе.

### Browser-based Logout

Для тестирования redirect-логаута откройте в браузере:

```
https://localhost:7237/api/auth/logout/{sessionId}
```

## Управление тестовыми пользователями (через Swagger UI)

В Swagger UI эндпоинты mock-сервера отображаются в секции **FakeEgisz**.

### Список fake-пользователей

Раскройте **`GET /realms/master/fake/users`** -> **Try it out** -> **Execute**.

### Переключение пользователя

Раскройте **`POST /realms/master/fake/switch-user`** -> **Try it out** -> **Execute**.

После переключения следующий вызов `/api/auth/login` авторизует другого пользователя.

## Доступные fake-пользователи

| Username | Email                | Имя  | Фамилия | Отчество   |
|----------|----------------------|------|---------|------------|
| ivanov   | ivanov@example.com   | Иван | Иванов  | Петрович   |
| petrov   | petrov@example.com   | Пётр | Петров  | Сергеевич  |

## Полный сценарий тестирования

```
1. docker-compose up -d
2. dotnet run --launch-profile https
3. Открыть https://localhost:7237/swagger
4. В браузере: /api/auth/login          -> получить sessionId
5. Swagger: GET /api/auth/session/{id}   -> проверить сессию
6. Swagger: GET /api/auth/userinfo/{id}  -> данные пользователя
7. Swagger: POST /api/auth/refresh       -> обновить токены
8. Swagger: POST /api/auth/logout        -> выход (Single Logout)
9. Swagger: GET /api/auth/session/{id}   -> убедиться что сессия неактивна
```

## Переход на реальную ИА ЕГИСЗ

Замените в `appsettings.json`:

```json
"EgiszOidc": {
  "Authority": "https://ia-test.egisz.rosminzdrav.ru/realms/master",
  "ClientId": "ваш-реальный-client-id",
  "ClientSecret": "ваш-реальный-client-secret"
}
```

`FakeEgiszController` не влияет на работу в Production -- Authority
будет указывать на реальный сервер ИА ЕГИСЗ.
