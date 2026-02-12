using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OIDC_test3.Configuration;
using OIDC_test3.Models;

namespace OIDC_test3.Services;

public interface IJwtTokenService
{
    string GenerateAccessToken(AppUser user);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateAccessToken(string token);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public string GenerateAccessToken(AppUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Sub),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("user_id", user.Id.ToString()),
            new("auth_provider", user.AuthProvider),
        };

        if (!string.IsNullOrEmpty(user.UserName))
            claims.Add(new Claim("preferred_username", user.UserName));
        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        if (!string.IsNullOrEmpty(user.GivenName))
            claims.Add(new Claim(JwtRegisteredClaimNames.GivenName, user.GivenName));
        if (!string.IsNullOrEmpty(user.FamilyName))
            claims.Add(new Claim(JwtRegisteredClaimNames.FamilyName, user.FamilyName));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var handler = new JwtSecurityTokenHandler();

        try
        {
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
        }
        catch
        {
            return null;
        }
    }
}
