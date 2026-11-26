using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.Exercises.Application;
using EnglishPlatform.Exercises.Contracts;
using EnglishPlatform.Exercises.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishPlatform.Exercises.Infrastructure;

/// <summary>Модуль Exercises. Форматы заданий, проверка ответов, журнал попыток.</summary>
public sealed class ExercisesModule : IModule
{
    public string Name => "Exercises";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ExercisesDb>(configuration, ExercisesDb.SchemaName);
        services.AddScoped<IExercisesDb>(sp => sp.GetRequiredService<ExercisesDb>());
        services.AddScoped<IExerciseEngine, ExerciseEngine>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
    }
}
