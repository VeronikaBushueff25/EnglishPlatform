using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
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
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
    }
}
