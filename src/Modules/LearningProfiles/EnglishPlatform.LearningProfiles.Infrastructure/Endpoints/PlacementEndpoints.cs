using EnglishPlatform.BuildingBlocks.Infrastructure.Http;
using EnglishPlatform.BuildingBlocks.Paging;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.LearningProfiles.Application;
using EnglishPlatform.LearningProfiles.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EnglishPlatform.LearningProfiles.Infrastructure.Endpoints;

internal static class PlacementEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        RouteGroupBuilder placement = api.MapGroup("/placement").WithTags("Placement").RequireAuthorization();

        placement.MapPost("/start", async (StartPlacementRequest? request, PlacementService service, CancellationToken ct) =>
                (await service.StartAsync(request?.Length, ct)).ToHttpResult())
            .WithName("StartPlacement")
            .Produces<PlacementStepDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        placement.MapPost("/{id:guid}/answers", async (Guid id, PlacementAnswerRequest request, PlacementService service, CancellationToken ct) =>
                (await service.AnswerAsync(id, request, ct)).ToHttpResult())
            .WithValidation<PlacementAnswerRequest>()
            .WithName("AnswerPlacement")
            .Produces<PlacementStepDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        placement.MapPost("/{id:guid}/finish", async (Guid id, PlacementService service, CancellationToken ct) =>
                (await service.FinishAsync(id, ct)).ToHttpResult())
            .WithName("FinishPlacement")
            .Produces<PlacementResultDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/me/plan", async (PlacementService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetPlanAsync(ct)))
            .WithTags("Placement")
            .RequireAuthorization()
            .WithName("GetLearningPlan");

        RouteGroupBuilder admin = api.MapGroup("/admin/placement-questions")
            .WithTags("Admin: placement")
            .RequireAuthorization(AppPolicies.Admin);

        admin.MapGet("/", async (Skill? skill, Cefr? level, int? page, int? pageSize, PlacementQuestionsAdmin service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(skill, level, new PageRequest(page ?? 1, pageSize ?? PageRequest.DefaultPageSize), ct)))
            .WithName("AdminListPlacementQuestions");

        admin.MapGet("/{id:guid}", async (Guid id, PlacementQuestionsAdmin service, CancellationToken ct) =>
                (await service.GetAsync(id, ct)).ToHttpResult())
            .WithName("AdminGetPlacementQuestion")
            .Produces<PlacementQuestionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/", async (SavePlacementQuestionRequest request, PlacementQuestionsAdmin service, CancellationToken ct) =>
                TypedResults.Ok(await service.CreateAsync(request, ct)))
            .WithValidation<SavePlacementQuestionRequest>()
            .WithName("AdminCreatePlacementQuestion");

        admin.MapPut("/{id:guid}", async (Guid id, SavePlacementQuestionRequest request, PlacementQuestionsAdmin service, CancellationToken ct) =>
                (await service.UpdateAsync(id, request, ct)).ToHttpResult())
            .WithValidation<SavePlacementQuestionRequest>()
            .WithName("AdminUpdatePlacementQuestion")
            .Produces<PlacementQuestionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapDelete("/{id:guid}", async (Guid id, PlacementQuestionsAdmin service, CancellationToken ct) =>
                (await service.DeleteAsync(id, ct)).ToHttpResult())
            .WithName("AdminDeletePlacementQuestion")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
