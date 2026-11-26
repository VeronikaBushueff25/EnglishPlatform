using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishPlatform.BuildingBlocks.Infrastructure.Modules;

/// <summary>
/// Точка входа модуля. Host знает только этот интерфейс: модуль сам регистрирует свои сервисы
/// и сам вешает свои маршруты на группу /api/v1.
/// </summary>
public interface IModule
{
    string Name { get; }

    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder api);
}
