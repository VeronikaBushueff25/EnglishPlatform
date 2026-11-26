using System.ComponentModel.DataAnnotations;
using EnglishPlatform.BuildingBlocks.Paging;
using EnglishPlatform.BuildingBlocks.Results;

namespace EnglishPlatform.Identity.Application;

public sealed record AdminUserDto(Guid Id, string Email, IReadOnlyCollection<string> Roles, DateTimeOffset CreatedAt, bool IsLockedOut);

public sealed record SetUserRoleRequest(
    [property: Required(ErrorMessage = "Укажите роль")]
    string Role);

public interface IAdminUserService
{
    Task<PagedResult<AdminUserDto>> ListAsync(string? search, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Назначает пользователю ровно одну роль из <see cref="BuildingBlocks.Security.Roles"/>.</summary>
    Task<Result> SetRoleAsync(Guid userId, string role, Guid actingAdminId, CancellationToken cancellationToken);
}

public static class AdminUserErrors
{
    public static readonly Error NotFound = Error.NotFound("users.not_found", "Пользователь не найден");

    public static readonly Error UnknownRole = Error.Validation("users.unknown_role", "Неизвестная роль");

    public static readonly Error CannotDemoteSelf =
        Error.Conflict("users.cannot_demote_self", "Нельзя снять роль администратора с самого себя");
}
