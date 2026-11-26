using EnglishPlatform.BuildingBlocks.Infrastructure.Http;
using EnglishPlatform.Users.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EnglishPlatform.Users.Infrastructure.Endpoints;

internal static class MeEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        RouteGroupBuilder me = api.MapGroup("/me").WithTags("Profile").RequireAuthorization();

        me.MapGet("/", async (ProfileService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetMeAsync(ct)))
            .WithName("GetMe");

        me.MapPut("/", async (UpdateMeRequest request, ProfileService service, CancellationToken ct) =>
                (await service.UpdateMeAsync(request, ct)).ToHttpResult())
            .WithValidation<UpdateMeRequest>()
            .WithName("UpdateMe")
            .Produces<MeResponse>();

        me.MapPut("/settings", async (UpdateSettingsRequest request, ProfileService service, CancellationToken ct) =>
                TypedResults.Ok(await service.UpdateSettingsAsync(request, ct)))
            .WithValidation<UpdateSettingsRequest>()
            .WithName("UpdateSettings");
    }
}
