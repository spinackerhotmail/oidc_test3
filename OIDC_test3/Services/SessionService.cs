using Microsoft.EntityFrameworkCore;
using OIDC_test3.Data;
using OIDC_test3.Models;

namespace OIDC_test3.Services;

public class SessionService : ISessionService
{
    private readonly AuthDbContext _db;

    public SessionService(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<UserSession> CreateSessionAsync(AppUser user, string accessToken, string refreshToken,
        string idToken, int expiresIn, int refreshExpiresIn, string? sessionState)
    {
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            IdToken = idToken,
            AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
            RefreshTokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshExpiresIn),
            SessionState = sessionState,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    public async Task<UserSession?> GetActiveSessionAsync(Guid sessionId)
    {
        return await _db.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.IsActive);
    }

    public async Task InvalidateSessionAsync(Guid sessionId)
    {
        var session = await _db.UserSessions.FindAsync(sessionId);
        if (session is not null)
        {
            session.IsActive = false;
            await _db.SaveChangesAsync();
        }
    }

    public async Task InvalidateAllUserSessionsAsync(Guid userId)
    {
        await _db.UserSessions
            .Where(s => s.UserId == userId && s.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
    }
}
