namespace OIDC_test3.Services;

public class EgiszAuthProvider : IAuthProvider
{
    private readonly IEgiszOidcClient _oidcClient;

    public EgiszAuthProvider(IEgiszOidcClient oidcClient)
    {
        _oidcClient = oidcClient;
    }

    public string ProviderName => "egisz";

    public async Task<AuthResultDto> AuthenticateAsync(AuthRequestContext context)
    {
        if (string.IsNullOrEmpty(context.AuthorizationCode) || string.IsNullOrEmpty(context.RedirectUri))
            throw new ArgumentException("Authorization code and redirect URI are required for EGISZ provider.");

        var tokenResponse = await _oidcClient.ExchangeCodeAsync(context.AuthorizationCode, context.RedirectUri);
        var userInfo = await _oidcClient.GetUserInfoAsync(tokenResponse.AccessToken);

        return new AuthResultDto
        {
            Sub = userInfo.Sub,
            UserName = userInfo.PreferredUsername,
            Email = userInfo.Email,
            GivenName = userInfo.GivenName,
            FamilyName = userInfo.FamilyName,
            MiddleName = userInfo.MiddleName,
            ProviderAccessToken = tokenResponse.AccessToken,
            ProviderRefreshToken = tokenResponse.RefreshToken,
            ProviderIdToken = tokenResponse.IdToken,
            SessionState = tokenResponse.SessionState,
            ProviderExpiresIn = tokenResponse.ExpiresIn,
            ProviderRefreshExpiresIn = tokenResponse.RefreshExpiresIn,
        };
    }

    public async Task<AuthResultDto> RefreshAsync(string providerRefreshToken, string userId)
    {
        var tokenResponse = await _oidcClient.RefreshTokenAsync(providerRefreshToken);

        return new AuthResultDto
        {
            Sub = userId,
            ProviderAccessToken = tokenResponse.AccessToken,
            ProviderRefreshToken = tokenResponse.RefreshToken,
            ProviderIdToken = tokenResponse.IdToken,
            SessionState = tokenResponse.SessionState,
            ProviderExpiresIn = tokenResponse.ExpiresIn,
            ProviderRefreshExpiresIn = tokenResponse.RefreshExpiresIn,
        };
    }

    public async Task LogoutAsync(string? providerRefreshToken, string? providerIdToken)
    {
        if (!string.IsNullOrEmpty(providerRefreshToken))
        {
            await _oidcClient.LogoutAsync(providerRefreshToken);
        }
    }
}
