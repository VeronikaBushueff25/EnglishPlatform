using EnglishPlatform.BuildingBlocks.Infrastructure.Http;
using EnglishPlatform.BuildingBlocks.Paging;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Vocabulary.Application;
using EnglishPlatform.Vocabulary.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EnglishPlatform.Vocabulary.Infrastructure.Endpoints;

internal static class VocabularyEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        MapCatalog(api);
        MapMyWords(api);
        MapAdmin(api);
    }

    private static void MapCatalog(IEndpointRouteBuilder api)
    {
        RouteGroupBuilder lexemes = api.MapGroup("/lexemes").WithTags("Vocabulary").RequireAuthorization();

        lexemes.MapGet("/", async (string? search, LexemeType? type, CefrLevel? level, Guid? categoryId, bool? noLevel, int? page, int? pageSize,
                    LexemeCatalog catalog, CancellationToken ct) =>
                TypedResults.Ok(await catalog.SearchAsync(new LexemeFilter(search, type, level, categoryId, noLevel ?? false), Page(page, pageSize), ct)))
            .WithName("ListLexemes");

        lexemes.MapGet("/{id:guid}", async (Guid id, LexemeCatalog catalog, CancellationToken ct) =>
                (await catalog.GetAsync(id, ct)).ToHttpResult())
            .WithName("GetLexeme")
            .Produces<LexemeDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/categories", async (LexemeCatalog catalog, CancellationToken ct) =>
                TypedResults.Ok(await catalog.GetCategoriesAsync(ct)))
            .WithTags("Vocabulary")
            .RequireAuthorization()
            .WithName("ListCategories");
    }

    private static void MapMyWords(IEndpointRouteBuilder api)
    {
        RouteGroupBuilder words = api.MapGroup("/my-words").WithTags("My words").RequireAuthorization();

        words.MapGet("/", async (string? search, LexemeType? type, CefrLevel? level, Guid? categoryId, bool? noLevel, int? page, int? pageSize,
                    MyWordsService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(new LexemeFilter(search, type, level, categoryId, noLevel ?? false), Page(page, pageSize), ct)))
            .WithName("ListMyWords");

        words.MapGet("/{id:guid}", async (Guid id, MyWordsService service, CancellationToken ct) =>
                (await service.GetAsync(id, ct)).ToHttpResult())
            .WithName("GetMyWord")
            .Produces<MyWordDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        words.MapPost("/", async (AddMyWordRequest request, MyWordsService service, CancellationToken ct) =>
                (await service.AddAsync(request, ct)).ToHttpResult())
            .WithValidation<AddMyWordRequest>()
            .WithName("AddMyWord")
            .Produces<MyWordDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        words.MapPut("/{id:guid}", async (Guid id, UpdateMyWordRequest request, MyWordsService service, CancellationToken ct) =>
                (await service.UpdateAsync(id, request, ct)).ToHttpResult())
            .WithValidation<UpdateMyWordRequest>()
            .WithName("UpdateMyWord")
            .Produces<MyWordDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        words.MapDelete("/{id:guid}", async (Guid id, MyWordsService service, CancellationToken ct) =>
                (await service.DeleteAsync(id, ct)).ToHttpResult())
            .WithName("DeleteMyWord")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        words.MapPost("/import", async (ImportCsvRequest request, MyWordsService service, CancellationToken ct) =>
                (await service.ImportAsync(request, ct)).ToHttpResult())
            .WithValidation<ImportCsvRequest>()
            .WithName("ImportMyWords")
            .Produces<ImportResult>();

        words.MapPut("/bulk", async (BulkMyWordsRequest request, MyWordsService service, CancellationToken ct) =>
                TypedResults.Ok(await service.BulkAsync(request, ct)))
            .WithValidation<BulkMyWordsRequest>()
            .WithName("BulkUpdateMyWords");
    }

    private static void MapAdmin(IEndpointRouteBuilder api)
    {
        RouteGroupBuilder lexemes = api.MapGroup("/admin/lexemes")
            .WithTags("Admin: vocabulary")
            .RequireAuthorization(AppPolicies.Admin);

        lexemes.MapPost("/", async (SaveLexemeRequest request, VocabularyAdminService service, CancellationToken ct) =>
                (await service.CreateLexemeAsync(request, ct)).ToHttpResult())
            .WithValidation<SaveLexemeRequest>()
            .WithName("AdminCreateLexeme")
            .Produces<LexemeDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        lexemes.MapPut("/{id:guid}", async (Guid id, SaveLexemeRequest request, VocabularyAdminService service, CancellationToken ct) =>
                (await service.UpdateLexemeAsync(id, request, ct)).ToHttpResult())
            .WithValidation<SaveLexemeRequest>()
            .WithName("AdminUpdateLexeme")
            .Produces<LexemeDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        lexemes.MapDelete("/{id:guid}", async (Guid id, VocabularyAdminService service, CancellationToken ct) =>
                (await service.DeleteLexemeAsync(id, ct)).ToHttpResult())
            .WithName("AdminDeleteLexeme")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        lexemes.MapPost("/import", async (ImportCsvRequest request, VocabularyAdminService service, CancellationToken ct) =>
                (await service.ImportLexemesAsync(request, ct)).ToHttpResult())
            .WithValidation<ImportCsvRequest>()
            .WithName("AdminImportLexemes")
            .Produces<ImportResult>();

        lexemes.MapPut("/bulk", async (BulkLexemesRequest request, VocabularyAdminService service, CancellationToken ct) =>
                (await service.BulkUpdateLexemesAsync(request, ct)).ToHttpResult())
            .WithValidation<BulkLexemesRequest>()
            .WithName("AdminBulkUpdateLexemes")
            .Produces<BulkResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        lexemes.MapPost("/from-my-words", async (VocabularyAdminService service, CancellationToken ct) =>
                TypedResults.Ok(await service.PublishMyWordsAsync(ct)))
            .WithName("AdminPublishMyWords");

        RouteGroupBuilder categories = api.MapGroup("/admin/categories")
            .WithTags("Admin: vocabulary")
            .RequireAuthorization(AppPolicies.Admin);

        categories.MapPost("/", async (SaveCategoryRequest request, VocabularyAdminService service, CancellationToken ct) =>
                (await service.CreateCategoryAsync(request, ct)).ToHttpResult())
            .WithValidation<SaveCategoryRequest>()
            .WithName("AdminCreateCategory")
            .Produces<CategoryDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        categories.MapPut("/{id:guid}", async (Guid id, SaveCategoryRequest request, VocabularyAdminService service, CancellationToken ct) =>
                (await service.UpdateCategoryAsync(id, request, ct)).ToHttpResult())
            .WithValidation<SaveCategoryRequest>()
            .WithName("AdminUpdateCategory")
            .Produces<CategoryDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        categories.MapDelete("/{id:guid}", async (Guid id, VocabularyAdminService service, CancellationToken ct) =>
                (await service.DeleteCategoryAsync(id, ct)).ToHttpResult())
            .WithName("AdminDeleteCategory")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static PageRequest Page(int? page, int? pageSize) => new(page ?? 1, pageSize ?? PageRequest.DefaultPageSize);
}
