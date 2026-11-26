using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.BuildingBlocks.Infrastructure.Events;
using EnglishPlatform.BuildingBlocks.Infrastructure.Security;
using EnglishPlatform.BuildingBlocks.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishPlatform.BuildingBlocks.Infrastructure.Modules;

public static class ModuleExtensions
{
    public static IServiceCollection AddModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyCollection<IModule> modules)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IEventBus, InProcessEventBus>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        foreach (IModule module in modules)
        {
            module.RegisterServices(services, configuration);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder api, IReadOnlyCollection<IModule> modules)
    {
        foreach (IModule module in modules)
        {
            module.MapEndpoints(api);
        }

        return api;
    }

    /// <summary>Выполняет <see cref="IModuleInitializer"/> всех модулей после миграций.</summary>
    public static async Task InitializeModulesAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        foreach (IModuleInitializer initializer in scope.ServiceProvider.GetServices<IModuleInitializer>())
        {
            await initializer.InitializeAsync(cancellationToken);
        }
    }
}
