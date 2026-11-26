using System.Security.Claims;
using EnglishPlatform.BuildingBlocks.Security;
using Microsoft.AspNetCore.Http;

namespace EnglishPlatform.BuildingBlocks.Infrastructure.Security;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        Guid.TryParse(Principal?.FindFirstValue(AppClaims.UserId), out Guid id)
            ? id
            : throw new InvalidOperationException("Запрос выполняется без аутентифицированного пользователя");

    public string? Email => Principal?.FindFirstValue(AppClaims.Email);

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(AppClaims.Role).Select(c => c.Value).ToArray() ?? [];
}
