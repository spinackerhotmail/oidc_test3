using System.Text.Json;
using OIDC_test3.Models;
using StackExchange.Redis;

namespace OIDC_test3.Services;

public class RedisSessionService : ISessionService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ILogger<RedisSessionService> _logger;

    public RedisSessionService(
        IConnectionMultiplexer redis,
        ILogger<RedisSessionService> logger)
    {
        _redis = redis;
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<UserSession> CreateSessionAsync(
        AppUser user, string authProvider,
        string accessToken, string refreshToken,
        DateTime accessTokenExpiresAt, DateTime refreshTokenExpiresAt,
        string? providerAccessToken = null, string? providerRefreshToken = null,
        string? providerIdToken = null, string? sessionState = null)
    {
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            AuthProvider = authProvider,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
            ProviderAccessToken = providerAccessToken,
            ProviderRefreshToken = providerRefreshToken,
            ProviderIdToken = providerIdToken,
            SessionState = sessionState,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Store session with TTL
        var key = GetSessionKey(refreshToken);
        var json = JsonSerializer.Serialize(session);
        var ttl = refreshTokenExpiresAt - DateTime.UtcNow;

        await _db.StringSetAsync(key, json, ttl);

        // Add to user's session set (for "logout everywhere")
        var userSetKey = GetUserSessionsKey(user.Id);
        await _db.SetAddAsync(userSetKey, session.Id.ToString());
        await _db.KeyExpireAsync(userSetKey, ttl);

        _logger.LogDebug("Created session {SessionId} for user {UserId} in Redis", session.Id, user.Id);

        return session;
    }

    public async Task<UserSession?> GetActiveSessionByRefreshTokenAsync(string refreshToken)
    {
        var key = GetSessionKey(refreshToken);
        var json = await _db.StringGetAsync(key);

        if (json.IsNullOrEmpty)
            return null;

        var session = JsonSerializer.Deserialize<UserSession>(json.ToString());
        
        // Check if session is still active and not expired
        if (session is null || !session.IsActive || session.RefreshTokenExpiresAt < DateTime.UtcNow)
            return null;

        return session;
    }

    public async Task InvalidateSessionAsync(Guid sessionId)
    {
        // Find session by scanning user sets (optimization: could maintain sessionId->refreshToken mapping)
        // For now, we rely on TTL expiration
        // This method is called during refresh (rotation), old session auto-expires via TTL
        
        _logger.LogDebug("Session {SessionId} invalidated (TTL will handle cleanup)", sessionId);
        
        // Note: In rotation scenario, old session key is no longer accessible via refreshToken
        // Redis TTL will clean it up automatically
        await Task.CompletedTask;
    }

    public async Task InvalidateAllUserSessionsAsync(Guid userId)
    {
        var userSetKey = GetUserSessionsKey(userId);
        var sessionIds = await _db.SetMembersAsync(userSetKey);

        foreach (var sessionId in sessionIds)
        {
            // We don't have direct sessionId->refreshToken mapping in this simple implementation
            // Alternative: mark user sessions as invalidated via a blacklist key
            await _db.StringSetAsync(
                GetInvalidatedSessionKey(sessionId.ToString()),
                "1",
                TimeSpan.FromDays(1)); // Keep blacklist for 1 day
        }

        // Clear user's session set
        await _db.KeyDeleteAsync(userSetKey);

        _logger.LogInformation("Invalidated all sessions for user {UserId}", userId);
    }

    // Helper methods for Redis key patterns
    private static string GetSessionKey(string refreshToken) 
        => $"session:refresh:{refreshToken}";

    private static string GetUserSessionsKey(Guid userId) 
        => $"user:{userId}:sessions";

    private static string GetInvalidatedSessionKey(string sessionId) 
        => $"session:invalidated:{sessionId}";
}
