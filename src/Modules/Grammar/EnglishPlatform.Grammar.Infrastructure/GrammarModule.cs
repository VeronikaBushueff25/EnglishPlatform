using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishPlatform.Grammar.Infrastructure;

/// <summary>Модуль Grammar. Грамматические темы и теги ошибок с шаблонами объяснений.</summary>
public sealed class GrammarModule : IModule
{
    public string Name => "Grammar";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
    }
}
