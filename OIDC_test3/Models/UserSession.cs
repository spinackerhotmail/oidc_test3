namespace OIDC_test3.Models;

public class UserSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>
    /// Authentication provider used: "local", "egisz".
    /// </summary>
    public string AuthProvider { get; set; } = "local";

    /// <summary>
    /// Session state value returned by IA EGISZ (only for egisz provider).
    /// </summary>
    public string? SessionState { get; set; }

    /// <summary>
    /// Provider-issued access token (EGISZ) or null (local).
    /// </summary>
    public string? ProviderAccessToken { get; set; }

    /// <summary>
    /// Provider-issued refresh token (EGISZ) or null (local).
    /// </summary>
    public string? ProviderRefreshToken { get; set; }

    /// <summary>
    /// Provider-issued ID token (EGISZ) or null (local).
    /// </summary>
    public string? ProviderIdToken { get; set; }

    /// <summary>
    /// Service-issued JWT access token.
    /// </summary>
    public string AccessToken { get; set; } = null!;

    /// <summary>
    /// Service-issued JWT refresh token.
    /// </summary>
    public string RefreshToken { get; set; } = null!;

    public DateTime AccessTokenExpiresAt { get; set; }
    public DateTime RefreshTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
