using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using OIDC_test3.Configuration;

namespace OIDC_test3.Services;

public class EgiszOidcClient : IEgiszOidcClient
{
    private readonly HttpClient _httpClient;
    private readonly EgiszOidcSettings _settings;
    private readonly ILogger<EgiszOidcClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public EgiszOidcClient(HttpClient httpClient, IOptions<EgiszOidcSettings> settings,
        ILogger<EgiszOidcClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public string BuildAuthorizeUrl(string redirectUri, string state)
    {
        var authority = _settings.Authority.TrimEnd('/');
        return $"{authority}/protocol/openid-connect/auth" +
               $"?response_type=code" +
               $"&client_id={Uri.EscapeDataString(_settings.ClientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&scope=openid%20profile%20email";
    }

    public async Task<EgiszTokenResponse> ExchangeCodeAsync(string code, string redirectUri)
    {
        var authority = _settings.Authority.TrimEnd('/');
        var tokenUrl = $"{authority}/protocol/openid-connect/token";

        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret
        };

        var response = await _httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(parameters));
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Token exchange failed: {StatusCode} {Content}", response.StatusCode, content);
            throw new HttpRequestException($"Token exchange failed with status {response.StatusCode}: {content}");
        }

        return JsonSerializer.Deserialize<EgiszTokenResponse>(content, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize token response.");
    }

    public async Task<EgiszTokenResponse> RefreshTokenAsync(string refreshToken)
    {
        var authority = _settings.Authority.TrimEnd('/');
        var tokenUrl = $"{authority}/protocol/openid-connect/token";

        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret
        };

        var response = await _httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(parameters));
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Token refresh failed: {StatusCode} {Content}", response.StatusCode, content);
            throw new HttpRequestException($"Token refresh failed with status {response.StatusCode}: {content}");
        }

        return JsonSerializer.Deserialize<EgiszTokenResponse>(content, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize token response.");
    }

    public async Task<EgiszUserInfo> GetUserInfoAsync(string accessToken)
    {
        var authority = _settings.Authority.TrimEnd('/');
        var userInfoUrl = $"{authority}/protocol/openid-connect/userinfo";

        var request = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("UserInfo request failed: {StatusCode} {Content}", response.StatusCode, content);
            throw new HttpRequestException($"UserInfo request failed with status {response.StatusCode}: {content}");
        }

        return JsonSerializer.Deserialize<EgiszUserInfo>(content, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize user info response.");
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var authority = _settings.Authority.TrimEnd('/');
        var logoutUrl = $"{authority}/protocol/openid-connect/logout";

        var parameters = new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret
        };

        var response = await _httpClient.PostAsync(logoutUrl, new FormUrlEncodedContent(parameters));

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Backend logout failed: {StatusCode} {Content}", response.StatusCode, content);
        }
    }

    public string BuildLogoutUrl(string? idTokenHint, string postLogoutRedirectUri)
    {
        var authority = _settings.Authority.TrimEnd('/');
        var url = $"{authority}/protocol/openid-connect/logout" +
                  $"?post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}";

        if (!string.IsNullOrEmpty(idTokenHint))
        {
            url += $"&id_token_hint={Uri.EscapeDataString(idTokenHint)}";
        }

        return url;
    }
}
