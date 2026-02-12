using OIDC_test3.Models;

namespace OIDC_test3.Services;

public interface IUserService
{
    Task<AppUser> GetOrCreateUserAsync(string sub, string? userName, string? email,
        string? givenName, string? familyName, string? middleName);
}
