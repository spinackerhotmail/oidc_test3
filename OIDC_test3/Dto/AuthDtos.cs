namespace OIDC_test3.Dto;

// --- Unified token response (same for all providers) ---
// Refresh token is sent via HttpOnly cookie, not in the response body.

public record TokenResponseDto(
    string AccessToken,
    int ExpiresIn,
    UserInfoDto User,
    string AuthProvider);

// --- Local auth ---

public record LocalLoginRequestDto(string Username, string Password);

public record RegisterRequestDto(
    string Username,
    string Password,
    string? Email = null,
    string? GivenName = null,
    string? FamilyName = null,
    string? MiddleName = null);

// --- EGISZ OIDC ---

public record LoginResponseDto(string RedirectUrl);

// --- Refresh / Logout ---
// Refresh token is read from HttpOnly cookie.

public record RefreshResponseDto(
    string AccessToken,
    int ExpiresIn);

// --- Session / UserInfo ---

public record UserInfoDto(
    string Sub,
    string? UserName,
    string? Email,
    string? GivenName,
    string? FamilyName,
    string? MiddleName,
    string? AuthProvider = null);

public record SessionInfoDto(
    Guid SessionId,
    bool IsActive,
    DateTime AccessTokenExpiresAt,
    UserInfoDto User);

// --- Providers ---

public record ProvidersResponseDto(IEnumerable<string> Providers);
