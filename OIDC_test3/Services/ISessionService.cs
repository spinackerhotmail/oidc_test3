using OIDC_test3.Models;

namespace OIDC_test3.Services;

public interface ISessionService
{
    Task<UserSession> CreateSessionAsync(AppUser user, string authProvider,
        string accessToken, string refreshToken,
        DateTime accessTokenExpiresAt, DateTime refreshTokenExpiresAt,
        string? providerAccessToken = null, string? providerRefreshToken = null,
        string? providerIdToken = null, string? sessionState = null);

    Task<UserSession?> GetActiveSessionByRefreshTokenAsync(string refreshToken);
    Task InvalidateSessionAsync(Guid sessionId);
    Task InvalidateAllUserSessionsAsync(Guid userId);
}
