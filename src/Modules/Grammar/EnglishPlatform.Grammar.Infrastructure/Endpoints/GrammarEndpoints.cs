using EnglishPlatform.BuildingBlocks.Infrastructure.Http;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Grammar.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EnglishPlatform.Grammar.Infrastructure.Endpoints;

internal static class GrammarEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        RouteGroupBuilder grammar = api.MapGroup("/grammar").WithTags("Grammar").RequireAuthorization();

        grammar.MapGet("/topics", async (GrammarService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListTopicsAsync(ct)))
            .WithName("ListGrammarTopics");

        grammar.MapGet("/topics/{slug}", async (string slug, GrammarService service, CancellationToken ct) =>
                (await service.GetTopicAsync(slug, ct)).ToHttpResult())
            .WithName("GetGrammarTopic")
            .Produces<GrammarTopicDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grammar.MapGet("/error-tags", async (GrammarService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListTagsAsync(ct)))
            .WithName("ListErrorTags");

        RouteGroupBuilder admin = api.MapGroup("/admin").WithTags("Admin: grammar").RequireAuthorization(AppPolicies.Admin);

        admin.MapPost("/grammar-topics", async (SaveGrammarTopicRequest request, GrammarService service, CancellationToken ct) =>
                (await service.CreateTopicAsync(request, ct)).ToHttpResult())
            .WithValidation<SaveGrammarTopicRequest>()
            .WithName("AdminCreateGrammarTopic")
            .Produces<GrammarTopicDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPut("/grammar-topics/{id:guid}", async (Guid id, SaveGrammarTopicRequest request, GrammarService service, CancellationToken ct) =>
                (await service.UpdateTopicAsync(id, request, ct)).ToHttpResult())
            .WithValidation<SaveGrammarTopicRequest>()
            .WithName("AdminUpdateGrammarTopic")
            .Produces<GrammarTopicDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapDelete("/grammar-topics/{id:guid}", async (Guid id, GrammarService service, CancellationToken ct) =>
                (await service.DeleteTopicAsync(id, ct)).ToHttpResult())
            .WithName("AdminDeleteGrammarTopic")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPut("/error-tags/{code}", async (string code, UpdateErrorTagRequest request, GrammarService service, CancellationToken ct) =>
                (await service.UpdateTagAsync(code, request, ct)).ToHttpResult())
            .WithValidation<UpdateErrorTagRequest>()
            .WithName("AdminUpdateErrorTag")
            .Produces<ErrorTagDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
