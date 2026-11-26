using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.BuildingBlocks.Security;
using EnglishPlatform.Identity.Application;
using EnglishPlatform.Identity.Infrastructure.Endpoints;
using EnglishPlatform.Identity.Infrastructure.Persistence;
using EnglishPlatform.Identity.Infrastructure.Services;
using EnglishPlatform.Identity.Infrastructure.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EnglishPlatform.Identity.Infrastructure;

/// <summary>Модуль Identity. Регистрация, вход, токены, роли.</summary>
public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<IdentityDb>(configuration, IdentityDb.Schema);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.Configure<IdentityModuleOptions>(configuration.GetSection(IdentityModuleOptions.Section));

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDb>()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<RussianIdentityErrorDescriber>();

        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(2));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = TokenIssuer.CreateKey(jwt.Value.SigningKey),
                    NameClaimType = AppClaims.UserId,
                    RoleClaimType = AppClaims.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AppPolicies.Admin, policy => policy.RequireRole(Roles.Admin));

        services.AddSingleton<TokenIssuer>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAuthEmailSender, LoggingEmailSender>();
        services.AddScoped<IModuleInitializer, IdentityInitializer>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        AuthEndpoints.Map(api);
        AdminUserEndpoints.Map(api);
    }
}
