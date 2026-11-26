using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.LearningProfiles.Application;
using EnglishPlatform.LearningProfiles.Infrastructure.Endpoints;
using EnglishPlatform.LearningProfiles.Infrastructure.Persistence;
using EnglishPlatform.LearningProfiles.Infrastructure.Seed;
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
        services.AddModuleDbContext<LearningProfilesDb>(configuration, LearningProfilesDb.SchemaName);
        services.AddScoped<ILearningProfilesDb>(sp => sp.GetRequiredService<LearningProfilesDb>());

        var options = new PlacementOptions();
        configuration.GetSection(PlacementOptions.Section).Bind(options);
        services.AddSingleton(options);
        services.AddScoped<PlacementService>();
        services.AddScoped<PlacementQuestionsAdmin>();
        services.AddScoped<EnglishPlatform.LearningProfiles.Contracts.ISkillLevelsReader, SkillLevelsReader>();
        services.AddScoped<IModuleInitializer, PlacementSeed>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        PlacementEndpoints.Map(api);
    }
}
