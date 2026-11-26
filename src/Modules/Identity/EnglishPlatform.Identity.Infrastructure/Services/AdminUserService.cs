using EnglishPlatform.BuildingBlocks.Paging;
using EnglishPlatform.BuildingBlocks.Results;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Identity.Application;
using EnglishPlatform.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EnglishPlatform.Identity.Infrastructure.Services;

internal sealed class AdminUserService(IdentityDb db, UserManager<AppUser> userManager, TimeProvider timeProvider) : IAdminUserService
{
    public async Task<PagedResult<AdminUserDto>> ListAsync(string? search, PageRequest page, CancellationToken cancellationToken)
    {
        IQueryable<AppUser> users = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string normalized = search.Trim().ToUpperInvariant();
            users = users.Where(u => u.NormalizedEmail!.Contains(normalized));
        }

        long total = await users.LongCountAsync(cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();

        var rows = await users
            .OrderBy(u => u.Email)
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.CreatedAt,
                u.LockoutEnd,
                Roles = db.UserRoles.Where(ur => ur.UserId == u.Id)
                    .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        AdminUserDto[] items = rows
            .Select(r => new AdminUserDto(r.Id, r.Email!, r.Roles, r.CreatedAt, r.LockoutEnd > now))
            .ToArray();

        return new PagedResult<AdminUserDto>(items, page.SafePage, page.SafePageSize, total);
    }

    public async Task<Result> SetRoleAsync(Guid userId, string role, Guid actingAdminId, CancellationToken cancellationToken)
    {
        if (!Roles.All.Contains(role))
        {
            return AdminUserErrors.UnknownRole;
        }

        if (userId == actingAdminId && role != Roles.Admin)
        {
            return AdminUserErrors.CannotDemoteSelf;
        }

        AppUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AdminUserErrors.NotFound;
        }

        IList<string> current = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, current.Where(r => r != role));
        if (!current.Contains(role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        // Новая роль попадёт в токен при следующем обновлении access-токена (не позже 15 минут).
        return Result.Success();
    }
}
