using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishPlatform.LearningProfiles.Infrastructure;

/// <summary>Модуль LearningProfiles. Тест уровня, уровни CEFR по навыкам, начальный план.</summary>
public sealed class LearningProfilesModule : IModule
{
    public string Name => "LearningProfiles";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
    }
}
