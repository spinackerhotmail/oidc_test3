namespace OIDC_test3.Configuration;

public class EgiszOidcSettings
{
    public const string SectionName = "EgiszOidc";

    /// <summary>
    /// IA EGISZ authority URL, e.g. https://ia-test.egisz.rosminzdrav.ru/realms/master
    /// </summary>
    public string Authority { get; set; } = null!;

    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;

    /// <summary>
    /// The callback path in this service where IA redirects after authentication.
    /// </summary>
    public string CallbackPath { get; set; } = "/api/auth/callback";

    /// <summary>
    /// The path IA redirects to after logout.
    /// </summary>
    public string SignedOutCallbackPath { get; set; } = "/api/auth/signedout";

    /// <summary>
    /// Where to redirect the user after successful login (front-end URL).
    /// </summary>
    public string PostLoginRedirectUri { get; set; } = "/";

    /// <summary>
    /// Where to redirect the user after logout (front-end URL).
    /// </summary>
    public string PostLogoutRedirectUri { get; set; } = "/";
}
