namespace OIDC_test3.Configuration;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = null!;
    public string Issuer { get; set; } = "EgiszAuthService";
    public string Audience { get; set; } = "EgiszAuthClients";
    public int AccessTokenExpirationMinutes { get; set; } = 30;
    public int RefreshTokenExpirationMinutes { get; set; } = 1440;
}
