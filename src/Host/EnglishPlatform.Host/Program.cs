using System.Reflection;
using EnglishPlatform.BuildingBlocks.Infrastructure.Modules;
using EnglishPlatform.BuildingBlocks.Infrastructure.Persistence;
using EnglishPlatform.Host;
using EnglishPlatform.Host.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using Serilog;

// Генерация OpenAPI при сборке запускает приложение без базы: в этом режиме миграции не трогаем.
bool isBuildTimeDocumentGeneration = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

if (isBuildTimeDocumentGeneration)
{
    // Документу ключ подписи не нужен, но без него не пройдёт проверка настроек при старте.
    builder.Configuration["Jwt:SigningKey"] ??= "build-time-openapi-generation-placeholder-key";
}

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApi();
builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);
builder.Services.AddObservability(builder.Configuration);
builder.Services.AddModules(builder.Configuration, ModuleCatalog.All);

// За nginx: схема и IP клиента берутся из X-Forwarded-*, иначе лимиты считались бы по IP прокси.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    // Сколько прокси стоит перед API: 1 — только nginx из compose; 2 — ещё Caddy/балансировщик с HTTPS перед ним.
    // Берём адрес клиента ровно на столько шагов назад, чтобы его нельзя было подделать своим заголовком.
    options.ForwardLimit = builder.Configuration.GetValue("ForwardedHeaders:ForwardLimit", 1);
});

WebApplication app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

// live: процесс жив; ready: можно пускать трафик (база доступна).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

RouteGroupBuilder api = app.MapGroup("/api/v1");
api.MapModules(ModuleCatalog.All);

if (!isBuildTimeDocumentGeneration)
{
    // Под блокировкой: одновременно стартующие экземпляры не мигрируют и не заполняют базу параллельно.
    await app.RunExclusiveDatabaseSetupAsync(async () =>
    {
        if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            await app.MigrateModuleDatabasesAsync();
        }

        await app.InitializeModulesAsync();
    });
}

await app.RunAsync();

/// <summary>Нужен WebApplicationFactory в интеграционных тестах.</summary>
public partial class Program;
