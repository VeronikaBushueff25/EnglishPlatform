using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.Grammar.Application;
using EnglishPlatform.Grammar.Contracts;
using EnglishPlatform.Grammar.Infrastructure.Endpoints;
using EnglishPlatform.Grammar.Infrastructure.Persistence;
using EnglishPlatform.Grammar.Infrastructure.Seed;
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
        services.AddModuleDbContext<GrammarDb>(configuration, GrammarDb.SchemaName);
        services.AddScoped<IGrammarDb>(sp => sp.GetRequiredService<GrammarDb>());
        services.AddScoped<GrammarService>();
        services.AddScoped<IGrammarHints, GrammarHints>();
        services.AddScoped<IModuleInitializer, GrammarInitializer>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        GrammarEndpoints.Map(api);
    }
}
