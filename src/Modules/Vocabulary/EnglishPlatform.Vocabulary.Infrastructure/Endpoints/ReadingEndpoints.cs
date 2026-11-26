using EnglishPlatform.BuildingBlocks.Infrastructure.Http;
using EnglishPlatform.BuildingBlocks.Paging;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Vocabulary.Application.Reading;
using EnglishPlatform.Vocabulary.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EnglishPlatform.Vocabulary.Infrastructure.Endpoints;

internal static class ReadingEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        RouteGroupBuilder reading = api.MapGroup("/reading").WithTags("Reading").RequireAuthorization();

        reading.MapGet("/texts", async (CefrLevel? level, int? page, int? pageSize, ReadingService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(level, all: false, new PageRequest(page ?? 1, pageSize ?? PageRequest.DefaultPageSize), ct)))
            .WithName("ListReadingTexts");

        reading.MapGet("/texts/{id:guid}", async (Guid id, ReadingService service, CancellationToken ct) =>
                (await service.GetAsync(id, allowHidden: false, ct)).ToHttpResult())
            .WithName("GetReadingText")
            .Produces<ReadingTextDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/my-words/texts", async (ReadingService service, CancellationToken ct) => TypedResults.Ok(await service.MyWordTextsAsync(ct)))
            .WithTags("My words")
            .RequireAuthorization()
            .WithName("ListMyWordTexts")
            .WithSummary("Тексты слов личного словаря — подчеркнуть знакомые при чтении");

        api.MapGet("/practice/words", async (int? count, ReadingService service, CancellationToken ct) =>
                TypedResults.Ok(await service.PracticeWordsAsync(count ?? 20, ct)))
            .WithTags("Practice")
            .RequireAuthorization()
            .WithName("GetPracticeWords")
            .WithSummary("Случайные слова личного словаря для игр (расписание повторений не меняется)");

        RouteGroupBuilder admin = api.MapGroup("/admin/reading-texts").WithTags("Admin: reading").RequireAuthorization(AppPolicies.Admin);

        admin.MapGet("/", async (CefrLevel? level, int? page, int? pageSize, ReadingService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(level, all: true, new PageRequest(page ?? 1, pageSize ?? PageRequest.DefaultPageSize), ct)))
            .WithName("AdminListReadingTexts");

        admin.MapGet("/{id:guid}", async (Guid id, ReadingService service, CancellationToken ct) =>
                (await service.GetAsync(id, allowHidden: true, ct)).ToHttpResult())
            .WithName("AdminGetReadingText")
            .Produces<ReadingTextDto>();

        admin.MapPost("/", async (SaveReadingTextRequest request, ReadingService service, CancellationToken ct) =>
                TypedResults.Ok(await service.CreateAsync(request, ct)))
            .WithValidation<SaveReadingTextRequest>()
            .WithName("AdminCreateReadingText");

        admin.MapPut("/{id:guid}", async (Guid id, SaveReadingTextRequest request, ReadingService service, CancellationToken ct) =>
                (await service.UpdateAsync(id, request, ct)).ToHttpResult())
            .WithValidation<SaveReadingTextRequest>()
            .WithName("AdminUpdateReadingText")
            .Produces<ReadingTextDto>();

        admin.MapDelete("/{id:guid}", async (Guid id, ReadingService service, CancellationToken ct) =>
                (await service.DeleteAsync(id, ct)).ToHttpResult())
            .WithName("AdminDeleteReadingText");
    }
}
