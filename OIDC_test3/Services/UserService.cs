using Microsoft.EntityFrameworkCore;
using OIDC_test3.Data;
using OIDC_test3.Models;

namespace OIDC_test3.Services;

public class UserService : IUserService
{
    private readonly AuthDbContext _db;

    public UserService(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<AppUser> GetOrCreateUserAsync(string sub, string? userName, string? email,
        string? givenName, string? familyName, string? middleName, string authProvider)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Sub == sub);
        if (user is null)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                Sub = sub,
                UserName = userName,
                Email = email,
                GivenName = givenName,
                FamilyName = familyName,
                MiddleName = middleName,
                AuthProvider = authProvider,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
        }
        else
        {
            user.UserName = userName ?? user.UserName;
            user.Email = email ?? user.Email;
            user.GivenName = givenName ?? user.GivenName;
            user.FamilyName = familyName ?? user.FamilyName;
            user.MiddleName = middleName ?? user.MiddleName;
            user.LastLoginAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<AppUser> CreateLocalUserAsync(string userName, string passwordHash,
        string? email, string? givenName, string? familyName, string? middleName)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Sub = $"local:{Guid.NewGuid()}",
            UserName = userName,
            PasswordHash = passwordHash,
            Email = email,
            GivenName = givenName,
            FamilyName = familyName,
            MiddleName = middleName,
            AuthProvider = "local",
            CreatedAt = DateTime.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<AppUser?> FindByUserNameAsync(string userName)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName);
    }

    public async Task<AppUser?> FindByIdAsync(Guid id)
    {
        return await _db.Users.FindAsync(id);
    }
}
