using EnglishPlatform.BuildingBlocks.Infrastructure.Http;
using EnglishPlatform.BuildingBlocks.Paging;
using EnglishPlatform.LearningSessions.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EnglishPlatform.LearningSessions.Infrastructure.Endpoints;

internal static class SessionEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/learning/today", async (SessionService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetTodayAsync(ct)))
            .WithTags("Learning")
            .RequireAuthorization()
            .WithName("GetLearningToday");

        RouteGroupBuilder sessions = api.MapGroup("/sessions").WithTags("Learning").RequireAuthorization();

        sessions.MapPost("/", async (StartSessionRequest? request, SessionService service, CancellationToken ct) =>
                TypedResults.Ok(await service.StartAsync(request?.Kind, ct)))
            .WithName("StartSession");

        sessions.MapGet("/", async (int? page, int? pageSize, SessionService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(new PageRequest(page ?? 1, pageSize ?? PageRequest.DefaultPageSize), ct)))
            .WithName("ListSessions");

        sessions.MapGet("/{id:guid}/next", async (Guid id, SessionService service, CancellationToken ct) =>
                (await service.NextAsync(id, ct)).ToHttpResult())
            .WithName("GetNextTask")
            .Produces<NextStepDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        sessions.MapPost("/{id:guid}/answers", async (Guid id, AnswerRequest request, SessionService service, CancellationToken ct) =>
                (await service.AnswerAsync(id, request, ct)).ToHttpResult())
            .WithValidation<AnswerRequest>()
            .WithName("AnswerTask")
            .Produces<AnswerResultDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sessions.MapPost("/{id:guid}/mute-audio", async (Guid id, int? minutes, SessionService service, CancellationToken ct) =>
                (await service.MuteAudioAsync(id, minutes, ct)).ToHttpResult())
            .WithName("MuteSessionAudio")
            .WithSummary("Не могу слушать: задания на слух выключаются на время, текущее заменяется")
            .Produces<NextStepDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        sessions.MapPost("/{id:guid}/finish", async (Guid id, SessionService service, CancellationToken ct) =>
                (await service.FinishAsync(id, ct)).ToHttpResult())
            .WithName("FinishSession")
            .Produces<SessionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
