using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OIDC_test3.Configuration;
using OIDC_test3.Dto;
using OIDC_test3.Services;

namespace OIDC_test3.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "refresh_token";

    private readonly IAuthProviderFactory _providerFactory;
    private readonly IEgiszOidcClient _oidcClient;
    private readonly IUserService _userService;
    private readonly ISessionService _sessionService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly EgiszOidcSettings _egiszSettings;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthProviderFactory providerFactory,
        IEgiszOidcClient oidcClient,
        IUserService userService,
        ISessionService sessionService,
        IJwtTokenService jwtTokenService,
        IOptions<EgiszOidcSettings> egiszSettings,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthController> logger)
    {
        _providerFactory = providerFactory;
        _oidcClient = oidcClient;
        _userService = userService;
        _sessionService = sessionService;
        _jwtTokenService = jwtTokenService;
        _egiszSettings = egiszSettings.Value;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns available authentication providers.
    /// </summary>
    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        return Ok(new ProvidersResponseDto(_providerFactory.GetAvailableProviders()));
    }

    // ======================== LOCAL AUTH ========================

    /// <summary>
    /// Register a new local user.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        var existingUser = await _userService.FindByUserNameAsync(request.Username);
        if (existingUser is not null)
            return Conflict(new { error = "Username already exists." });

        var passwordHash = LocalAuthProvider.HashPassword(request.Password);
        var user = await _userService.CreateLocalUserAsync(
            request.Username, passwordHash, request.Email,
            request.GivenName, request.FamilyName, request.MiddleName);

        _logger.LogInformation("Local user {UserName} registered (Sub: {Sub}).", user.UserName, user.Sub);

        return Ok(new UserInfoDto(user.Sub, user.UserName, user.Email,
            user.GivenName, user.FamilyName, user.MiddleName, "local"));
    }

    /// <summary>
    /// Authenticate with local username and password. Returns service-issued JWT tokens.
    /// </summary>
    [HttpPost("login/local")]
    public async Task<IActionResult> LoginLocal([FromBody] LocalLoginRequestDto request)
    {
        var provider = _providerFactory.GetProvider("local");

        AuthResultDto authResult;
        try
        {
            authResult = await provider.AuthenticateAsync(new AuthRequestContext
            {
                Username = request.Username,
                Password = request.Password,
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Invalid username or password." });
        }

        var user = await _userService.GetOrCreateUserAsync(
            authResult.Sub, authResult.UserName, authResult.Email,
            authResult.GivenName, authResult.FamilyName, authResult.MiddleName, "local");

        return Ok(await CreateTokenResponseAsync(user, "local"));
    }

    // ======================== EGISZ OIDC ========================

    /// <summary>
    /// Initiates the OIDC Authorization Code flow by redirecting to IA EGISZ.
    /// </summary>
    [HttpGet("login/egisz")]
    public IActionResult LoginEgisz([FromQuery] string? returnUrl)
    {
        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/auth/callback/egisz";
        var state = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(returnUrl ?? _egiszSettings.PostLoginRedirectUri));
        var authorizeUrl = _oidcClient.BuildAuthorizeUrl(callbackUrl, state);

        return Redirect(authorizeUrl);
    }

    /// <summary>
    /// OIDC callback -- exchanges authorization code for tokens. Returns service-issued JWT tokens.
    /// </summary>
    [HttpGet("callback/egisz")]
    public async Task<IActionResult> CallbackEgisz([FromQuery] string code, [FromQuery] string state)
    {
        if (string.IsNullOrEmpty(code))
            return BadRequest(new { error = "Authorization code is missing." });

        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/auth/callback/egisz";
        var provider = _providerFactory.GetProvider("egisz");

        var authResult = await provider.AuthenticateAsync(new AuthRequestContext
        {
            AuthorizationCode = code,
            RedirectUri = callbackUrl,
        });

        var user = await _userService.GetOrCreateUserAsync(
            authResult.Sub, authResult.UserName, authResult.Email,
            authResult.GivenName, authResult.FamilyName, authResult.MiddleName, "egisz");

        _logger.LogInformation("EGISZ user {Sub} authenticated.", user.Sub);

        return Ok(await CreateTokenResponseAsync(user, "egisz",
            authResult.ProviderAccessToken, authResult.ProviderRefreshToken,
            authResult.ProviderIdToken, authResult.SessionState));
    }

    // ======================== UNIFIED ENDPOINTS ========================

    /// <summary>
    /// Refresh access token using refresh token from HttpOnly cookie. Works for all providers.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var cookieRefreshToken = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrEmpty(cookieRefreshToken))
            return Unauthorized(new { error = "Refresh token cookie is missing." });

        var session = await _sessionService.GetActiveSessionByRefreshTokenAsync(cookieRefreshToken);
        if (session is null)
            return Unauthorized(new { error = "Invalid or expired refresh token." });

        if (session.RefreshTokenExpiresAt < DateTime.UtcNow)
        {
            await _sessionService.InvalidateSessionAsync(session.Id);
            DeleteRefreshTokenCookie();
            return Unauthorized(new { error = "Refresh token expired." });
        }

        // Invalidate old session
        await _sessionService.InvalidateSessionAsync(session.Id);

        // For EGISZ provider, also refresh provider tokens
        string? providerAccessToken = null, providerRefreshToken = null, providerIdToken = null;
        if (session.AuthProvider == "egisz" && !string.IsNullOrEmpty(session.ProviderRefreshToken))
        {
            try
            {
                var provider = _providerFactory.GetProvider("egisz");
                var refreshResult = await provider.RefreshAsync(session.ProviderRefreshToken, session.User.Sub);
                providerAccessToken = refreshResult.ProviderAccessToken;
                providerRefreshToken = refreshResult.ProviderRefreshToken;
                providerIdToken = refreshResult.ProviderIdToken;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EGISZ token refresh failed, issuing local-only tokens.");
            }
        }

        var user = session.User;
        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        await _sessionService.CreateSessionAsync(
            user, session.AuthProvider,
            accessToken, refreshToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            DateTime.UtcNow.AddMinutes(_jwtSettings.RefreshTokenExpirationMinutes),
            providerAccessToken, providerRefreshToken, providerIdToken);

        SetRefreshTokenCookie(refreshToken);

        _logger.LogInformation("Session refreshed for user {Sub}.", user.Sub);

        return Ok(new RefreshResponseDto(
            accessToken,
            _jwtSettings.AccessTokenExpirationMinutes * 60));
    }

    /// <summary>
    /// Returns info about the current authenticated user. Requires Bearer token.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _userService.FindByIdAsync(userId);
        if (user is null)
            return NotFound(new { error = "User not found." });

        return Ok(new UserInfoDto(user.Sub, user.UserName, user.Email,
            user.GivenName, user.FamilyName, user.MiddleName, user.AuthProvider));
    }

    /// <summary>
    /// Logout -- reads refresh token from HttpOnly cookie, invalidates session, clears cookie.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var cookieRefreshToken = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrEmpty(cookieRefreshToken))
            return NotFound(new { error = "Refresh token cookie is missing." });

        var session = await _sessionService.GetActiveSessionByRefreshTokenAsync(cookieRefreshToken);
        if (session is null)
        {
            DeleteRefreshTokenCookie();
            return NotFound(new { error = "Session not found or already logged out." });
        }

        await _sessionService.InvalidateAllUserSessionsAsync(session.UserId);

        // Notify provider about logout
        var provider = _providerFactory.GetProvider(session.AuthProvider);
        await provider.LogoutAsync(session.ProviderRefreshToken, session.ProviderIdToken);

        DeleteRefreshTokenCookie();

        _logger.LogInformation("User {Sub} logged out via {Provider}.", session.User.Sub, session.AuthProvider);


        return Ok(new { message = "Logged out successfully." });
    }

    // ======================== HELPERS ========================

    private async Task<TokenResponseDto> CreateTokenResponseAsync(
        Models.AppUser user, string authProvider,
        string? providerAccessToken = null, string? providerRefreshToken = null,
        string? providerIdToken = null, string? sessionState = null)
    {
        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        await _sessionService.CreateSessionAsync(
            user, authProvider,
            accessToken, refreshToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            DateTime.UtcNow.AddMinutes(_jwtSettings.RefreshTokenExpirationMinutes),
            providerAccessToken, providerRefreshToken, providerIdToken, sessionState);

        SetRefreshTokenCookie(refreshToken);

        return new TokenResponseDto(
            accessToken,
            _jwtSettings.AccessTokenExpirationMinutes * 60,
            new UserInfoDto(user.Sub, user.UserName, user.Email,
                user.GivenName, user.FamilyName, user.MiddleName, authProvider),
            authProvider);
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            MaxAge = TimeSpan.FromMinutes(_jwtSettings.RefreshTokenExpirationMinutes)
        });
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth"
        });
    }
}
