using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.SpacedRepetition.Application;
using EnglishPlatform.SpacedRepetition.Contracts;
using EnglishPlatform.SpacedRepetition.Domain;
using EnglishPlatform.SpacedRepetition.Infrastructure.Persistence;
using EnglishPlatform.Vocabulary.Contracts;
using EnglishPlatform.BuildingBlocks.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishPlatform.SpacedRepetition.Infrastructure;

/// <summary>Модуль SpacedRepetition. Карточки и расписание повторений по FSRS.</summary>
public sealed class SpacedRepetitionModule : IModule
{
    public string Name => "SpacedRepetition";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SpacedRepetitionDb>(configuration, SpacedRepetitionDb.SchemaName);
        services.AddScoped<ISpacedRepetitionDb>(sp => sp.GetRequiredService<SpacedRepetitionDb>());

        double retention = configuration.GetValue("SpacedRepetition:DesiredRetention", 0.9);
        services.AddSingleton(new FsrsScheduler(new FsrsParameters { DesiredRetention = retention }));
        services.AddSingleton(new ProblemOptions { ProblemLapses = configuration.GetValue("SpacedRepetition:ProblemLapses", 2) });
        services.AddScoped<IReviewScheduler, ReviewScheduler>();
        services.AddScoped<IReviewStats, ReviewStats>();
        services.AddScoped<IIntegrationEventHandler<UserWordAdded>, CreateCardOnUserWordAdded>();
        services.AddScoped<IIntegrationEventHandler<UserWordDeleted>, RemoveCardsOnUserWordDeleted>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        api.MapGet("/my-words/progress", async (IReviewStats stats, ICurrentUser user, IConfiguration configuration, CancellationToken ct) =>
                TypedResults.Ok(await stats.GetWordProgressAsync(user.UserId, configuration.GetValue("Statistics:ActiveStabilityDays", 21.0), ct)))
            .WithTags("My words")
            .RequireAuthorization()
            .WithName("ListMyWordProgress")
            .WithSummary("Прогресс каждого слова: этап (новое, учу, узнаю, использую), повторения, ошибки, следующее повторение");
    }
}
