using EnglishPlatform.Analytics.Application;
using EnglishPlatform.Analytics.Contracts;
using EnglishPlatform.Analytics.Infrastructure.Persistence;
using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.LearningSessions.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishPlatform.Analytics.Infrastructure;

/// <summary>Модуль Analytics. Счётчики ошибок, дневные агрегаты, статистика.</summary>
public sealed class AnalyticsModule : IModule
{
    public string Name => "Analytics";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<AnalyticsDb>(configuration, AnalyticsDb.SchemaName);
        services.AddScoped<IAnalyticsDb>(sp => sp.GetRequiredService<AnalyticsDb>());

        var options = new WeaknessOptions();
        configuration.GetSection(WeaknessOptions.Section).Bind(options);
        services.AddSingleton(options);
        services.AddScoped<IWeaknessReader, WeaknessReader>();
        services.AddScoped<WeakSpotsService>();

        var stats = new StatsOptions();
        configuration.GetSection(StatsOptions.Section).Bind(stats);
        services.AddSingleton(stats);
        services.AddScoped<StatsService>();
        services.AddScoped<IIntegrationEventHandler<AnswerChecked>, CountAnswerChecked>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        api.MapGet("/stats/weak-spots", async (WeakSpotsService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetAsync(ct)))
            .WithTags("Statistics")
            .RequireAuthorization()
            .WithName("GetWeakSpots");

        api.MapGet("/stats/summary", async (StatsService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetSummaryAsync(ct)))
            .WithTags("Statistics")
            .RequireAuthorization()
            .WithName("GetStatsSummary");
    }
}
