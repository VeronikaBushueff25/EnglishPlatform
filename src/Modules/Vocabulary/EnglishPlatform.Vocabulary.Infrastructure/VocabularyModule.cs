using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.Vocabulary.Application;
using EnglishPlatform.Vocabulary.Infrastructure.Endpoints;
using EnglishPlatform.Vocabulary.Infrastructure.Persistence;
using EnglishPlatform.Vocabulary.Infrastructure.Seed;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishPlatform.Vocabulary.Infrastructure;

/// <summary>Модуль Vocabulary. Общая база лексики и личный словарь.</summary>
public sealed class VocabularyModule : IModule
{
    public string Name => "Vocabulary";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<VocabularyDb>(configuration, VocabularyDb.SchemaName);
        services.AddScoped<IVocabularyDb>(sp => sp.GetRequiredService<VocabularyDb>());
        services.AddScoped<LexemeCatalog>();
        services.AddScoped<MyWordsService>();
        services.AddScoped<VocabularyAdminService>();
        services.AddScoped<IModuleInitializer, VocabularyInitializer>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        VocabularyEndpoints.Map(api);
    }
}
