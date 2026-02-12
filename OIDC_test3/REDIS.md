# Redis для сессий пользователей

## Почему Redis?

? **Скорость** — In-memory операции быстрее PostgreSQL на порядок  
? **TTL** — Автоматическое удаление истёкших сессий (не нужна очистка)  
? **Масштабирование** — Легко добавить Redis Cluster при росте нагрузки  
? **Меньше нагрузки на PostgreSQL** — БД используется только для Users

## Структура данных в Redis

### Ключи

```
session:refresh:{refreshToken}  ? JSON объект UserSession (TTL = RefreshTokenExpiration)
user:{userId}:sessions          ? Set<Guid> активных sessionId (TTL = RefreshTokenExpiration)
session:invalidated:{sessionId} ? "1" (blacklist для logout, TTL = 1 день)
```

### Пример

После `POST /api/auth/login/local`:

```redis
SET session:refresh:abc123... '{"Id":"...","UserId":"...","IsActive":true,...}' EX 86400
SADD user:550e8400-e29b-41d4-a716-446655440000:sessions 123e4567-...
EXPIRE user:550e8400-e29b-41d4-a716-446655440000:sessions 86400
```

После `POST /api/auth/logout`:

```redis
SMEMBERS user:550e8400-e29b-41d4-a716-446655440000:sessions  # получить все sessionId
SET session:invalidated:123e4567-... "1" EX 86400           # blacklist
DEL user:550e8400-e29b-41d4-a716-446655440000:sessions       # удалить set
```

## Настройка

### `appsettings.json`

```json
"ConnectionStrings": {
  "AuthDb": "Host=localhost;Port=54320;...",
  "Redis": "localhost:6379"
}
```

### `docker-compose.yml`

```yaml
redis:
  image: redis:7-alpine
  ports:
    - "6379:6379"
  command: redis-server --appendonly yes
  volumes:
    - redisdata:/data
```

### Production

Для production используйте:
- **Пароль**: `redis-server --requirepass <password>`
- **TLS**: Через `stunnel` или AWS ElastiCache / Azure Cache for Redis
- **Replic: Добавьте Redis Sentinel или Cluster

Connection string с паролем:

```json
"Redis": "localhost:6379,password=mySecretPassword,ssl=true"
```

## Мониторинг

### Проверить сессии в Redis

```bash
docker exec -it egisz-auth-redis redis-cli

# Все ключи сессий
KEYS session:refresh:*

# Кол-во активных сессий
DBSIZE

# Инфо о памяти
INFO memory

# Получить сессию
GET session:refresh:abc123...

# Проверить TTL
TTL session:refresh:abc123...
```

### Очистить все сессии (для тестирования)

```bash
docker exec -it egisz-auth-redis redis-cli FLUSHDB
```

## Миграция с PostgreSQL

Если у вас уже были сессии в PostgreSQL:

1. **Применить миграцию** (удалить таблицу `user_sessions`):
   ```bash
   cd OIDC_test3
   dotnet ef migrations add RemoveUserSessionsTable
   dotnet ef database update
   ```

2. **Перезапустить сервис** — все новые сессии создаются в Redis

3. **Старые сессии** в PostgreSQL автоматически станут невалидными (refresh вернёт 401)

## Производительность

| Операция | PostgreSQL | Redis | Ускорение |
|----------|------------|-------|-----------|
| `CreateSession` | ~5-10ms | ~1ms | **5-10x** |
| `GetSessionByRefreshToken` | ~3-7ms | ~0.5ms | **6-14x** |
| `InvalidateSession` | ~5ms | ~0.5ms | **10x** |

В production с высокой нагрузкой разница может достигать **100x**.
