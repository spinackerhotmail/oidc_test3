namespace OIDC_test3.Models;

public class AppUser
{
    public Guid Id { get; set; }

    /// <summary>
    /// Subject identifier. For EGISZ users -- OIDC "sub" claim. For local users -- generated GUID string.
    /// </summary>
    public string Sub { get; set; } = null!;

    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? MiddleName { get; set; }

    /// <summary>
    /// BCrypt password hash. Only set for local auth provider users.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Authentication provider that created this user: "local", "egisz".
    /// </summary>
    public string AuthProvider { get; set; } = "local";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}
