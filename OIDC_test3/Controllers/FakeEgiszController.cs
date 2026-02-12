using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace OIDC_test3.Controllers;

/// <summary>
/// Fake IA EGISZ OIDC server for local development and testing.
/// Simulates all OIDC endpoints: well-known, authorize, token, userinfo, logout, certs.
/// Enabled only in Development environment.
/// </summary>
[ApiController]
[Route("realms/master")]
public class FakeEgiszController : ControllerBase
{
    private static readonly Dictionary<string, FakeUser> AuthCodes = new();
    private static readonly Dictionary<string, FakeUser> ActiveTokens = new();
    private static readonly Dictionary<string, FakeUser> RefreshTokens = new();

    private static readonly FakeUser[] FakeUsers =
    [
        new("acd969f9-7fe1-4230-b572-aa2c0d9b7009", "ivanov", "ivanov@example.com",
            "\u0418\u0432\u0430\u043D", "\u0418\u0432\u0430\u043D\u043E\u0432", "\u041F\u0435\u0442\u0440\u043E\u0432\u0438\u0447"),
        new("b1e23456-aaaa-bbbb-cccc-dd1234567890", "petrov", "petrov@example.com",
            "\u041F\u0451\u0442\u0440", "\u041F\u0435\u0442\u0440\u043E\u0432", "\u0421\u0435\u0440\u0433\u0435\u0435\u0432\u0438\u0447"),
    ];

    private static int _userIndex;

    /// <summary>
    /// OpenID Connect Discovery endpoint.
    /// </summary>
    [HttpGet(".well-known/openid-configuration")]
    public IActionResult WellKnown()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}/realms/master";
        return Ok(new
        {
            issuer = baseUrl,
            authorization_endpoint = $"{baseUrl}/protocol/openid-connect/auth",
            token_endpoint = $"{baseUrl}/protocol/openid-connect/token",
            token_introspection_endpoint = $"{baseUrl}/protocol/openid-connect/token/introspect",
            userinfo_endpoint = $"{baseUrl}/protocol/openid-connect/userinfo",
            end_session_endpoint = $"{baseUrl}/protocol/openid-connect/logout",
            jwks_uri = $"{baseUrl}/protocol/openid-connect/certs",
            grant_types_supported = new[] { "authorization_code", "refresh_token", "password", "client_credentials" },
            response_types_supported = new[] { "code", "id_token", "id_token token", "code id_token" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" },
            response_modes_supported = new[] { "query", "fragment", "form_post" },
        });
    }

    /// <summary>
    /// Authorization endpoint -- simulates the login page and redirects back with a code.
    /// In real IA EGISZ, this shows a login form. Here we auto-authorize a fake user.
    /// </summary>
    [HttpGet("protocol/openid-connect/auth")]
    public IActionResult Authorize(
        [FromQuery] string response_type,
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string? state,
        [FromQuery] string? scope)
    {
        // Pick a fake user (round-robin for SSO testing with multiple users)
        var user = FakeUsers[_userIndex % FakeUsers.Length];

        // Generate authorization code
        var code = Guid.NewGuid().ToString("N");
        AuthCodes[code] = user;

        // Build redirect URI with code and state
        var separator = redirect_uri.Contains('?') ? "&" : "?";
        var redirectUrl = $"{redirect_uri}{separator}code={code}";
        if (!string.IsNullOrEmpty(state))
            redirectUrl += $"&state={Uri.EscapeDataString(state)}";

        return Redirect(redirectUrl);
    }

    /// <summary>
    /// Token endpoint -- exchanges authorization code or refresh token for tokens.
    /// </summary>
    [HttpPost("protocol/openid-connect/token")]
    public IActionResult Token(
        [FromForm] string grant_type,
        [FromForm] string? code,
        [FromForm] string? refresh_token,
        [FromForm] string? client_id,
        [FromForm] string? client_secret,
        [FromForm] string? redirect_uri,
        [FromForm] string? username,
        [FromForm] string? password)
    {
        FakeUser? user = null;

        switch (grant_type)
        {
            case "authorization_code":
                if (string.IsNullOrEmpty(code) || !AuthCodes.TryGetValue(code, out user))
                    return BadRequest(new { error = "invalid_grant", error_description = "Invalid authorization code." });
                AuthCodes.Remove(code);
                break;

            case "refresh_token":
                if (string.IsNullOrEmpty(refresh_token) || !RefreshTokens.TryGetValue(refresh_token, out user))
                    return BadRequest(new { error = "invalid_grant", error_description = "Invalid refresh token." });
                RefreshTokens.Remove(refresh_token);
                break;

            case "password":
                user = FakeUsers.FirstOrDefault(u => u.PreferredUsername == username);
                if (user is null)
                    return BadRequest(new { error = "invalid_grant", error_description = "Invalid credentials." });
                break;

            default:
                return BadRequest(new { error = "unsupported_grant_type" });
        }

        // Generate tokens
        var accessToken = $"fake-access-{Guid.NewGuid():N}";
        var newRefreshToken = $"fake-refresh-{Guid.NewGuid():N}";
        var idToken = $"fake-id-{Guid.NewGuid():N}";
        var sessionState = Guid.NewGuid().ToString();

        ActiveTokens[accessToken] = user;
        RefreshTokens[newRefreshToken] = user;

        return Ok(new
        {
            access_token = accessToken,
            expires_in = 300,
            refresh_expires_in = 1800,
            refresh_token = newRefreshToken,
            token_type = "bearer",
            id_token = idToken,
            session_state = sessionState,
        });
    }

    /// <summary>
    /// UserInfo endpoint -- returns fake user profile data.
    /// </summary>
    [HttpGet("protocol/openid-connect/userinfo")]
    public IActionResult UserInfo()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return Unauthorized(new { error = "invalid_token" });

        var token = authHeader["Bearer ".Length..];
        if (!ActiveTokens.TryGetValue(token, out var user))
            return Unauthorized(new { error = "invalid_token", error_description = "Token not found or expired." });

        return Ok(new
        {
            sub = user.Sub,
            name = $"{user.GivenName} {user.MiddleName} {user.FamilyName}",
            preferred_username = user.PreferredUsername,
            given_name = user.GivenName,
            family_name = user.FamilyName,
            middle_name = user.MiddleName,
            email = user.Email,
        });
    }

    /// <summary>
    /// Logout endpoint (POST) -- invalidates refresh token.
    /// </summary>
    [HttpPost("protocol/openid-connect/logout")]
    public IActionResult LogoutPost([FromForm] string? refresh_token)
    {
        if (!string.IsNullOrEmpty(refresh_token))
            RefreshTokens.Remove(refresh_token);

        return NoContent();
    }

    /// <summary>
    /// Logout endpoint (GET) -- browser redirect-based logout.
    /// </summary>
    [HttpGet("protocol/openid-connect/logout")]
    public IActionResult LogoutGet(
        [FromQuery] string? post_logout_redirect_uri,
        [FromQuery] string? id_token_hint)
    {
        if (!string.IsNullOrEmpty(post_logout_redirect_uri))
            return Redirect(post_logout_redirect_uri);

        return Ok(new { message = "Logged out." });
    }

    /// <summary>
    /// JWKS endpoint -- returns a fake RSA public key for token signature validation.
    /// </summary>
    [HttpGet("protocol/openid-connect/certs")]
    public IActionResult Certs()
    {
        return Ok(new
        {
            keys = new[]
            {
                new
                {
                    kid = "fake-key-id-001",
                    kty = "RSA",
                    alg = "RS256",
                    use = "sig",
                    n = "0vx7agoebGcQSuuPiLJXZptN9nndrQmbXEps2aiAFbWhM78LhWx4cbbfAAtVT86zwu1RK7aPFFxuhDR1L6tSoc_BJECPebWKRXjBZCiFV4n3oknjhMstn64tZ_2W-5JsGY4Hc5n9yBXArwl93lqt7_RN5w6Cf0h4QyQ5v-65YGjQR0_FDW2QvzqY368QQMicAtaSqzs8KJZgnYb9c7d0zgdAZHzu6qMQvRL5hajrn1n91CbOpbISD08qNLyrdkt-bFTWhAI4vMQFh6WeZu0fM4lFd2NcRwr3XPksINHaQ-G_xBniIqbw0Ls1jF44-csFCur-kEgU8awapJzKnqDKgw",
                    e = "AQAB",
                }
            }
        });
    }

    /// <summary>
    /// Introspection endpoint -- checks if a token is active.
    /// </summary>
    [HttpPost("protocol/openid-connect/token/introspect")]
    public IActionResult Introspect([FromForm] string? token)
    {
        if (string.IsNullOrEmpty(token))
            return Ok(new { active = false });

        if (ActiveTokens.TryGetValue(token, out var user))
        {
            return Ok(new
            {
                active = true,
                sub = user.Sub,
                username = user.PreferredUsername,
                email = user.Email,
                token_type = "Bearer",
            });
        }

        return Ok(new { active = false });
    }

    /// <summary>
    /// Helper: switch to the next fake user (for testing multi-user SSO scenarios).
    /// POST /realms/master/fake/switch-user
    /// </summary>
    [HttpPost("fake/switch-user")]
    public IActionResult SwitchUser()
    {
        _userIndex++;
        var next = FakeUsers[_userIndex % FakeUsers.Length];
        return Ok(new
        {
            message = $"Switched to user: {next.PreferredUsername}",
            sub = next.Sub,
            username = next.PreferredUsername
        });
    }

    /// <summary>
    /// Helper: list all available fake users.
    /// GET /realms/master/fake/users
    /// </summary>
    [HttpGet("fake/users")]
    public IActionResult ListUsers()
    {
        var current = FakeUsers[_userIndex % FakeUsers.Length];
        return Ok(new
        {
            currentUser = current.PreferredUsername,
            users = FakeUsers.Select(u => new
            {
                u.Sub,
                u.PreferredUsername,
                u.Email,
                u.GivenName,
                u.FamilyName,
                u.MiddleName
            })
        });
    }

    private record FakeUser(
        string Sub,
        string PreferredUsername,
        string Email,
        string GivenName,
        string FamilyName,
        string MiddleName);
}
