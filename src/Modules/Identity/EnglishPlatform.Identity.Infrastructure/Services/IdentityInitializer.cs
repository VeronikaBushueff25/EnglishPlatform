using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Identity.Contracts;
using EnglishPlatform.Identity.Infrastructure.Persistence;
using EnglishPlatform.Identity.Infrastructure.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnglishPlatform.Identity.Infrastructure.Services;

/// <summary>Создаёт роли и первого администратора из конфигурации Identity:AdminEmail / AdminPassword.</summary>
internal sealed class IdentityInitializer(
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<AppUser> userManager,
    IEventBus eventBus,
    IOptions<IdentityModuleOptions> options,
    TimeProvider timeProvider,
    ILogger<IdentityInitializer> logger) : IModuleInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        foreach (string role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() });
            }
        }

        string? email = options.Value.AdminEmail;
        string? password = options.Value.AdminPassword;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        AppUser? admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new AppUser { Id = Guid.CreateVersion7(), Email = email, UserName = email, CreatedAt = timeProvider.GetUtcNow() };
            IdentityResult created = await userManager.CreateAsync(admin, password);
            if (!created.Succeeded)
            {
                logger.LogError("Не удалось создать администратора {Email}: {Errors}", email, string.Join("; ", created.Errors.Select(e => e.Description)));
                return;
            }

            await eventBus.PublishAsync(new UserRegistered(admin.Id, email, admin.CreatedAt), cancellationToken);
            logger.LogInformation("Создан администратор {Email}", email);
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
        {
            await userManager.AddToRoleAsync(admin, Roles.Admin);
        }
    }
}
