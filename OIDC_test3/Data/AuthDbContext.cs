using Microsoft.EntityFrameworkCore;
using OIDC_test3.Models;

namespace OIDC_test3.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    // UserSessions moved to Redis

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Sub).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Sub).IsUnique();
            entity.Property(e => e.UserName).HasMaxLength(256);
            entity.HasIndex(e => e.UserName).IsUnique().HasFilter("\"UserName\" IS NOT NULL");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.GivenName).HasMaxLength(256);
            entity.Property(e => e.FamilyName).HasMaxLength(256);
            entity.Property(e => e.MiddleName).HasMaxLength(256);
            entity.Property(e => e.PasswordHash).HasMaxLength(512);
            entity.Property(e => e.AuthProvider).IsRequired().HasMaxLength(50).HasDefaultValue("local");
        });
    }
}
