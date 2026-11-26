using EnglishPlatform.BuildingBlocks.Infrastructure.Http;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Vocabulary.Application.Dictionary;
using EnglishPlatform.Vocabulary.Infrastructure.Dictionary;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EnglishPlatform.Vocabulary.Infrastructure.Endpoints;

internal static class DictionaryEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/dictionary/lookup", async (string text, DictionaryLookup lookup, CancellationToken ct) =>
                await lookup.LookupAsync(text, ct) is { } result ? Results.Ok(result) : Results.NotFound())
            .WithTags("Dictionary")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Dictionary)
            .WithName("LookupWord")
            .WithSummary("Слово по тексту: из общей базы, а если его там нет — из внешнего словаря (и в базу)")
            .Produces<LookupResultDto>()
            .Produces(StatusCodes.Status404NotFound);

        RouteGroupBuilder suggestions = api.MapGroup("/my-words/suggestions").WithTags("My words").RequireAuthorization();

        suggestions.MapGet("/", async (int? offset, int? limit, WordSuggestions service, CancellationToken ct) =>
                TypedResults.Ok(await service.SuggestAsync(offset ?? 0, limit ?? 15, ct)))
            .RequireRateLimiting(RateLimitPolicies.Dictionary)
            .WithName("SuggestMyWords")
            .WithSummary("Что улучшить в личном словаре: уровень, транскрипция, тип, начальная форма (порциями)");

        suggestions.MapPost("/apply", async (ApplySuggestionsRequest request, WordSuggestions service, CancellationToken ct) =>
                TypedResults.Ok(await service.ApplyAsync(request, ct)))
            .WithValidation<ApplySuggestionsRequest>()
            .WithName("ApplyMyWordSuggestions");

        RouteGroupBuilder admin = api.MapGroup("/admin/dictionary").WithTags("Admin: vocabulary").RequireAuthorization(AppPolicies.Admin);

        admin.MapGet("/import", async (CoreVocabularyImport import, CancellationToken ct) =>
                TypedResults.Ok(import.Status with { BaseSize = await import.BaseSizeAsync(ct) }))
            .WithName("GetCoreImport");

        admin.MapPost("/import", (StartCoreImportRequest request, CoreVocabularyImport import) =>
                import.Start(request.Levels, request.Limit) ? Results.Accepted() : Results.Conflict())
            .WithValidation<StartCoreImportRequest>()
            .WithName("StartCoreImport")
            .WithSummary("Пополнить общую базу словами списков CEFR выбранных уровней (в фоне)")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status409Conflict);

        admin.MapDelete("/import", (CoreVocabularyImport import) =>
            {
                import.Cancel();
                return Results.NoContent();
            })
            .WithName("CancelCoreImport");
    }
}
