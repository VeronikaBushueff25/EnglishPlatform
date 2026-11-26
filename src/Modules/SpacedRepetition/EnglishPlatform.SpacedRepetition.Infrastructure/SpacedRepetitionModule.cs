using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.SpacedRepetition.Application;
using EnglishPlatform.SpacedRepetition.Contracts;
using EnglishPlatform.SpacedRepetition.Domain;
using EnglishPlatform.SpacedRepetition.Infrastructure.Persistence;
using EnglishPlatform.Vocabulary.Contracts;
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
    }
}
