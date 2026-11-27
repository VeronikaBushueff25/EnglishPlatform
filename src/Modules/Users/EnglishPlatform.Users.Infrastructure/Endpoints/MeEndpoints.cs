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

        me.MapPost("/supporter", async (ProfileService service, CancellationToken ct) =>
                TypedResults.Ok(await service.MarkSupporterAsync(ct)))
            .WithName("MarkSupporter")
            .WithSummary("Пользователь поддержал проект (отмечает сам) — значок и сердечко для питомца");

        // Ссылки «Поддержать проект» — открыты и гостям.
        api.MapGet("/support", (Microsoft.Extensions.Options.IOptions<SupportOptions> options) =>
            {
                SupportOptions o = options.Value;
                static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                static int? Number(string? value) => int.TryParse(value, out int n) && n >= 0 ? n : null;
                return TypedResults.Ok(new SupportInfoDto(Text(o.CloudTipsUrl), Text(o.BoostyUrl), Text(o.GoalTitle), Number(o.GoalAmount), Number(o.GoalRaised)));
            })
            .WithTags("Support")
            .AllowAnonymous()
            .WithName("GetSupportInfo");

        me.MapPut("/settings", async (UpdateSettingsRequest request, ProfileService service, CancellationToken ct) =>
                TypedResults.Ok(await service.UpdateSettingsAsync(request, ct)))
            .WithValidation<UpdateSettingsRequest>()
            .WithName("UpdateSettings");
    }
}
