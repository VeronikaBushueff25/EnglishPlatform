using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishPlatform.Ai.Infrastructure;

/// <summary>Модуль Ai. Интерфейсы AI-провайдеров. В MVP только заглушка.</summary>
public sealed class AiModule : IModule
{
    public string Name => "Ai";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
    }
}
