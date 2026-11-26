using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.LearningSessions.Application;
using EnglishPlatform.LearningSessions.Infrastructure.Endpoints;
using EnglishPlatform.LearningSessions.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishPlatform.LearningSessions.Infrastructure;

/// <summary>Модуль LearningSessions. Учебные сессии и их история.</summary>
public sealed class LearningSessionsModule : IModule
{
    public string Name => "LearningSessions";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<LearningSessionsDb>(configuration, LearningSessionsDb.SchemaName);
        services.AddScoped<ILearningSessionsDb>(sp => sp.GetRequiredService<LearningSessionsDb>());

        var options = new SessionOptions();
        configuration.GetSection(SessionOptions.Section).Bind(options);
        services.AddSingleton(options);
        services.AddScoped<SessionService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        SessionEndpoints.Map(api);
    }
}
