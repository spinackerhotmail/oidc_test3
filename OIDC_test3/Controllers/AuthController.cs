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
    private readonly IEgiszOidcClient _oidcClient;
    private readonly IUserService _userService;
    private readonly ISessionService _sessionService;
    private readonly EgiszOidcSettings _settings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IEgiszOidcClient oidcClient,
        IUserService userService,
        ISessionService sessionService,
        IOptions<EgiszOidcSettings> settings,
        ILogger<AuthController> logger)
    {
        _oidcClient = oidcClient;
        _userService = userService;
        _sessionService = sessionService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Initiates the OIDC Authorization Code flow by redirecting to IA EGISZ.
    /// </summary>
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/auth/callback";
        var state = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(returnUrl ?? _settings.PostLoginRedirectUri));
        var authorizeUrl = _oidcClient.BuildAuthorizeUrl(callbackUrl, state);

        return Redirect(authorizeUrl);
    }

    /// <summary>
    /// OIDC callback – exchanges authorization code for tokens, creates/updates user and session.
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        if (string.IsNullOrEmpty(code))
            return BadRequest(new { error = "Authorization code is missing." });

        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/auth/callback";

        // Exchange code for tokens
        var tokenResponse = await _oidcClient.ExchangeCodeAsync(code, callbackUrl);

        // Get user info from IA EGISZ
        var userInfo = await _oidcClient.GetUserInfoAsync(tokenResponse.AccessToken);

        // Create or update user in local DB
        var user = await _userService.GetOrCreateUserAsync(
            userInfo.Sub,
            userInfo.PreferredUsername,
            userInfo.Email,
            userInfo.GivenName,
            userInfo.FamilyName,
            userInfo.MiddleName);

        // Create session
        var session = await _sessionService.CreateSessionAsync(
            user,
            tokenResponse.AccessToken,
            tokenResponse.RefreshToken,
            tokenResponse.IdToken,
            tokenResponse.ExpiresIn,
            tokenResponse.RefreshExpiresIn,
            tokenResponse.SessionState);

        _logger.LogInformation("User {Sub} authenticated, session {SessionId} created.", user.Sub, session.Id);

        // Decode return URL from state
        string returnUrl;
        try
        {
            returnUrl = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(state));
        }
        catch
        {
            returnUrl = _settings.PostLoginRedirectUri;
        }

        // Return session info as JSON (can also redirect for browser-based flow)
        var response = new CallbackResponseDto(
            session.Id,
            tokenResponse.AccessToken,
            tokenResponse.ExpiresIn,
            tokenResponse.RefreshToken,
            tokenResponse.IdToken,
            new UserInfoDto(user.Sub, user.UserName, user.Email,
                user.GivenName, user.FamilyName, user.MiddleName));

        return Ok(response);
    }

    /// <summary>
    /// Returns info about the current session.
    /// </summary>
    [HttpGet("session/{sessionId:guid}")]
    public async Task<IActionResult> GetSession(Guid sessionId)
    {
        var session = await _sessionService.GetActiveSessionAsync(sessionId);
        if (session is null)
            return NotFound(new { error = "Session not found or inactive." });

        var user = session.User;
        return Ok(new SessionInfoDto(
            session.Id,
            session.IsActive,
            session.AccessTokenExpiresAt,
            new UserInfoDto(user.Sub, user.UserName, user.Email,
                user.GivenName, user.FamilyName, user.MiddleName)));
    }

    /// <summary>
    /// Refreshes access token using refresh token stored in the session.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto request)
    {
        var session = await _sessionService.GetActiveSessionAsync(request.SessionId);
        if (session is null)
            return NotFound(new { error = "Session not found or inactive." });

        if (string.IsNullOrEmpty(session.RefreshToken))
            return BadRequest(new { error = "No refresh token available for this session." });

        var tokenResponse = await _oidcClient.RefreshTokenAsync(session.RefreshToken);

        // Invalidate old session, create new one
        await _sessionService.InvalidateSessionAsync(session.Id);
        var newSession = await _sessionService.CreateSessionAsync(
            session.User,
            tokenResponse.AccessToken,
            tokenResponse.RefreshToken,
            tokenResponse.IdToken,
            tokenResponse.ExpiresIn,
            tokenResponse.RefreshExpiresIn,
            tokenResponse.SessionState);

        _logger.LogInformation("Session {OldSession} refreshed -> {NewSession}.", session.Id, newSession.Id);

        return Ok(new RefreshResponseDto(
            tokenResponse.AccessToken,
            tokenResponse.ExpiresIn,
            tokenResponse.RefreshToken,
            tokenResponse.IdToken));
    }

    /// <summary>
    /// Retrieves user info from IA EGISZ using the session's access token.
    /// </summary>
    [HttpGet("userinfo/{sessionId:guid}")]
    public async Task<IActionResult> GetUserInfo(Guid sessionId)
    {
        var session = await _sessionService.GetActiveSessionAsync(sessionId);
        if (session is null)
            return NotFound(new { error = "Session not found or inactive." });

        if (string.IsNullOrEmpty(session.AccessToken))
            return BadRequest(new { error = "No access token available." });

        var userInfo = await _oidcClient.GetUserInfoAsync(session.AccessToken);

        return Ok(new UserInfoDto(
            userInfo.Sub,
            userInfo.PreferredUsername,
            userInfo.Email,
            userInfo.GivenName,
            userInfo.FamilyName,
            userInfo.MiddleName));
    }

    /// <summary>
    /// Performs Single Logout: invalidates local session and calls IA EGISZ logout endpoint.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request)
    {
        var session = await _sessionService.GetActiveSessionAsync(request.SessionId);
        if (session is null)
            return NotFound(new { error = "Session not found or inactive." });

        // Invalidate all sessions for this user (Single Logout)
        await _sessionService.InvalidateAllUserSessionsAsync(session.UserId);

        // Notify IA EGISZ about logout
        if (!string.IsNullOrEmpty(session.RefreshToken))
        {
            await _oidcClient.LogoutAsync(session.RefreshToken);
        }

        _logger.LogInformation("User {Sub} logged out, all sessions invalidated.", session.User.Sub);

        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>
    /// Browser-based logout: redirects user to IA EGISZ logout endpoint for global session termination.
    /// </summary>
    [HttpGet("logout/{sessionId:guid}")]
    public async Task<IActionResult> LogoutRedirect(Guid sessionId)
    {
        var session = await _sessionService.GetActiveSessionAsync(sessionId);
        if (session is null)
            return NotFound(new { error = "Session not found or inactive." });

        await _sessionService.InvalidateAllUserSessionsAsync(session.UserId);

        var postLogoutUri = $"{Request.Scheme}://{Request.Host}{_settings.PostLogoutRedirectUri}";
        var logoutUrl = _oidcClient.BuildLogoutUrl(session.IdToken, postLogoutUri);

        return Redirect(logoutUrl);
    }
}
