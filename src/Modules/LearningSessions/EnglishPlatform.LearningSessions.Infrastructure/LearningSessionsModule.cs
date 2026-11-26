using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
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
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
    }
}
