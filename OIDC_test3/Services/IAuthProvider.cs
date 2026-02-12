using OIDC_test3.Dto;

namespace OIDC_test3.Services;

public interface IAuthProvider
{
    string ProviderName { get; }
    Task<AuthResultDto> AuthenticateAsync(AuthRequestContext context);
    Task<AuthResultDto> RefreshAsync(string providerRefreshToken, string userId);
    Task LogoutAsync(string? providerRefreshToken, string? providerIdToken);
}

public class AuthRequestContext
{
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? AuthorizationCode { get; init; }
    public string? RedirectUri { get; init; }
}

public class AuthResultDto
{
    public required string Sub { get; init; }
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public string? GivenName { get; init; }
    public string? FamilyName { get; init; }
    public string? MiddleName { get; init; }
    public string? ProviderAccessToken { get; init; }
    public string? ProviderRefreshToken { get; init; }
    public string? ProviderIdToken { get; init; }
    public string? SessionState { get; init; }
    public int ProviderExpiresIn { get; init; }
    public int ProviderRefreshExpiresIn { get; init; }
}
