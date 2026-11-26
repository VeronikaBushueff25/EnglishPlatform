using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.Users.Application;
using EnglishPlatform.Users.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishPlatform.Users.Infrastructure.Persistence;

public sealed class UsersDb(DbContextOptions<UsersDb> options) : ModuleDbContext(options), IUsersDb
{
    public const string SchemaName = "users";

    public override string Schema => SchemaName;

    public DbSet<UserProfile> Profiles => Set<UserProfile>();
}

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> profile)
    {
        profile.ToTable("user_profiles");
        profile.HasKey(p => p.Id);
        profile.Property(p => p.Id).HasColumnName("user_id").ValueGeneratedNever();
        profile.Property(p => p.DisplayName).HasMaxLength(UserProfile.MaxDisplayNameLength);
        profile.Property(p => p.InterfaceLanguage).HasConversion<string>().HasMaxLength(8);
        profile.Property(p => p.Plan).HasConversion<string>().HasMaxLength(16);
        profile.Property(p => p.TimeZone).HasMaxLength(64);

        // Цели храним массивом строк PostgreSQL: так их видно в базе и легко расширять.
        profile.PrimitiveCollection(p => p.Goals).ElementType(e => e.HasConversion<string>());
    }
}
