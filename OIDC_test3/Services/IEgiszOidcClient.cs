namespace OIDC_test3.Services;

public class EgiszTokenResponse
{
    public string AccessToken { get; set; } = null!;
    public int ExpiresIn { get; set; }
    public string RefreshToken { get; set; } = null!;
    public int RefreshExpiresIn { get; set; }
    public string IdToken { get; set; } = null!;
    public string TokenType { get; set; } = null!;
    public string? SessionState { get; set; }
}

public class EgiszUserInfo
{
    public string Sub { get; set; } = null!;
    public string? Name { get; set; }
    public string? PreferredUsername { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? MiddleName { get; set; }
    public string? Email { get; set; }
}

public interface IEgiszOidcClient
{
    string BuildAuthorizeUrl(string redirectUri, string state);
    Task<EgiszTokenResponse> ExchangeCodeAsync(string code, string redirectUri);
    Task<EgiszTokenResponse> RefreshTokenAsync(string refreshToken);
    Task<EgiszUserInfo> GetUserInfoAsync(string accessToken);
    Task LogoutAsync(string refreshToken);
    string BuildLogoutUrl(string? idTokenHint, string postLogoutRedirectUri);
}
