using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.Vocabulary.Application;
using EnglishPlatform.Vocabulary.Contracts;
using EnglishPlatform.Vocabulary.Application.Dictionary;
using EnglishPlatform.Vocabulary.Infrastructure.Dictionary;
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
        services.AddScoped<IUserWordsReader, UserWordsReader>();
        services.AddScoped<IStarterWords, StarterWords>();
        services.AddScoped<IModuleInitializer, VocabularyInitializer>();
        services.AddScoped<IModuleInitializer, ReadingSeed>();
        services.AddScoped<EnglishPlatform.Vocabulary.Application.Reading.ReadingService>();

        // Внешний словарь (Викисловарь через kaikki.org), уровни CEFR, пополнение базы.
        var dictionary = new DictionaryOptions();
        configuration.GetSection(DictionaryOptions.Section).Bind(dictionary);
        services.AddSingleton(dictionary);
        services.AddHttpClient(WiktionaryDictionarySource.HttpClientName, client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EnglishPlatform/1.0 (vocabulary lookup)"));
        services.AddScoped<IDictionarySource, WiktionaryDictionarySource>();
        services.AddScoped<IExampleSource, TatoebaExampleSource>();
        services.AddSingleton<ICefrLevels>(_ => CefrLevelResource.Load());
        services.AddScoped<DictionaryLookup>();
        services.AddScoped<WordSuggestions>();
        services.AddSingleton<CoreVocabularyImport>();
        services.AddHostedService(sp => sp.GetRequiredService<CoreVocabularyImport>());
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        VocabularyEndpoints.Map(api);
        DictionaryEndpoints.Map(api);
        ReadingEndpoints.Map(api);
    }
}
