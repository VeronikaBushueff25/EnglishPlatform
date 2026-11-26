using EnglishPlatform.BuildingBlocks.Events;
using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.Identity.Contracts;
using EnglishPlatform.Users.Application;
using EnglishPlatform.Users.Contracts;
using EnglishPlatform.Users.Infrastructure.Endpoints;
using EnglishPlatform.Users.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishPlatform.Users.Infrastructure;

/// <summary>Модуль Users. Профиль пользователя и настройки обучения.</summary>
public sealed class UsersModule : IModule
{
    public string Name => "Users";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<UsersDb>(configuration, UsersDb.SchemaName);
        services.AddScoped<IUsersDb>(sp => sp.GetRequiredService<UsersDb>());
        services.AddScoped<ProfileService>();
        services.AddScoped<IUserLearningSettings, UserLearningSettingsQuery>();
        services.AddScoped<IIntegrationEventHandler<UserRegistered>, CreateProfileOnUserRegistered>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        MeEndpoints.Map(api);
    }
}
