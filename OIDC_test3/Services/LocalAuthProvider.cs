using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OIDC_test3.Data;

namespace OIDC_test3.Services;

public class LocalAuthProvider : IAuthProvider
{
    private readonly AuthDbContext _db;

    public LocalAuthProvider(AuthDbContext db)
    {
        _db = db;
    }

    public string ProviderName => "local";

    public async Task<AuthResultDto> AuthenticateAsync(AuthRequestContext context)
    {
        if (string.IsNullOrEmpty(context.Username) || string.IsNullOrEmpty(context.Password))
            throw new ArgumentException("Username and password are required for local provider.");

        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.UserName == context.Username && u.AuthProvider == "local");

        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid username or password.");

        if (!VerifyPassword(context.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid username or password.");

        return new AuthResultDto
        {
            Sub = user.Sub,
            UserName = user.UserName,
            Email = user.Email,
            GivenName = user.GivenName,
            FamilyName = user.FamilyName,
            MiddleName = user.MiddleName,
        };
    }

    public Task<AuthResultDto> RefreshAsync(string providerRefreshToken, string userId)
    {
        return Task.FromResult(new AuthResultDto { Sub = userId });
    }

    public Task LogoutAsync(string? providerRefreshToken, string? providerIdToken)
    {
        return Task.CompletedTask;
    }

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        var result = new byte[salt.Length + hash.Length];
        salt.CopyTo(result, 0);
        hash.CopyTo(result, salt.Length);
        return Convert.ToBase64String(result);
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        var decoded = Convert.FromBase64String(storedHash);
        if (decoded.Length != 48) return false;

        var salt = decoded[..16];
        var storedHashBytes = decoded[16..];
        var computedHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);

        return CryptographicOperations.FixedTimeEquals(storedHashBytes, computedHash);
    }
}
