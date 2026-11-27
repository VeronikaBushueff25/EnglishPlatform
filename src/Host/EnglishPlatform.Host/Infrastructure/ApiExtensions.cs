using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using EnglishPlatform.BuildingBlocks.Infrastructure.Http;
using Microsoft.OpenApi;

namespace EnglishPlatform.Host.Infrastructure;

internal static class ApiExtensions
{
    /// <summary>JSON, OpenAPI со схемой Bearer и ограничение частоты для эндпоинтов входа.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Owlish API";
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access-токен из /api/v1/auth/login",
            };
            document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
            return Task.CompletedTask;
        }));

        // Лимит в памяти процесса: при нескольких экземплярах каждый считает свой. Для MVP достаточно,
        // общий лимит через Redis — когда он появится (см. план, раздел «Инфраструктура»).
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
            options.AddPolicy(RateLimitPolicies.Dictionary, context => RateLimitPartition.GetSlidingWindowLimiter(
                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? context.User.FindFirst("sub")?.Value
                    ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new SlidingWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6 }));
        });

        return services;
    }
}
