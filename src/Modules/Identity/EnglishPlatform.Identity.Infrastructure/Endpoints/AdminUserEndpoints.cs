using EnglishPlatform.BuildingBlocks.Infrastructure.Http;
using EnglishPlatform.BuildingBlocks.Paging;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Identity.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EnglishPlatform.Identity.Infrastructure.Endpoints;

internal static class AdminUserEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        RouteGroupBuilder admin = api.MapGroup("/admin/users")
            .WithTags("Admin: users")
            .RequireAuthorization(AppPolicies.Admin);

        admin.MapGet("/", async (string? search, int? page, int? pageSize, IAdminUserService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(search, new PageRequest(page ?? 1, pageSize ?? PageRequest.DefaultPageSize), ct)))
            .WithName("AdminListUsers");

        admin.MapPut("/{id:guid}/role", async (Guid id, SetUserRoleRequest request, IAdminUserService service, ICurrentUser currentUser, CancellationToken ct) =>
                (await service.SetRoleAsync(id, request.Role, currentUser.UserId, ct)).ToHttpResult())
            .WithValidation<SetUserRoleRequest>()
            .WithName("AdminSetUserRole")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
