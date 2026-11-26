using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishPlatform.SpacedRepetition.Infrastructure;

/// <summary>Модуль SpacedRepetition. Карточки и расписание повторений по FSRS.</summary>
public sealed class SpacedRepetitionModule : IModule
{
    public string Name => "SpacedRepetition";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
    }
}
