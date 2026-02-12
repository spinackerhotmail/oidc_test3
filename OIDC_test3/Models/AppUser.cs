namespace OIDC_test3.Models;

public class AppUser
{
    public Guid Id { get; set; }

    /// <summary>
    /// Subject identifier from IA EGISZ (OIDC "sub" claim).
    /// </summary>
    public string Sub { get; set; } = null!;

    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? MiddleName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public ICollection<UserSession> Sessions { get; set; } = [];
}
