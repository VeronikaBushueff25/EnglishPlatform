using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
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
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
    }
}
