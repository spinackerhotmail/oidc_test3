using OIDC_test3.Models;

namespace OIDC_test3.Services;

public interface ISessionService
{
    Task<UserSession> CreateSessionAsync(AppUser user, string accessToken, string refreshToken,
        string idToken, int expiresIn, int refreshExpiresIn, string? sessionState);

    Task<UserSession?> GetActiveSessionAsync(Guid sessionId);
    Task InvalidateSessionAsync(Guid sessionId);
    Task InvalidateAllUserSessionsAsync(Guid userId);
}
