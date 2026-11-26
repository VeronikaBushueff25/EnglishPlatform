using EnglishPlatform.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Identity.Infrastructure.Persistence;

public sealed class AppUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Контекст модуля Identity на базе ASP.NET Core Identity, схема identity.</summary>
public sealed class IdentityDb(DbContextOptions<IdentityDb> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public const string Schema = "identity";

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<AppUser>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        builder.Entity<RefreshToken>(token =>
        {
            token.ToTable("refresh_tokens");
            token.HasKey(t => t.Id);
            token.Property(t => t.Id).ValueGeneratedNever();
            token.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasIndex(t => new { t.UserId, t.ExpiresAt });
            token.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcConverter>();
    }

    private sealed class UtcConverter()
        : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, DateTimeOffset>(
            v => v.ToUniversalTime(), v => v.ToUniversalTime());
}
