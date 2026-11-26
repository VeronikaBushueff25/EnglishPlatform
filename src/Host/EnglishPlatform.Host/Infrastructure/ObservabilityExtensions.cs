using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EnglishPlatform.Host.Infrastructure;

internal static class ObservabilityExtensions
{
    /// <summary>
    /// Метрики и трассировка OpenTelemetry. Экспорт по OTLP включается, только если задан
    /// OTEL_EXPORTER_OTLP_ENDPOINT, иначе приложение работает без коллектора.
    /// </summary>
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        bool exportEnabled = !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("english-platform-api"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation()
                    .AddSource("Npgsql");

                if (exportEnabled)
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentationIfAvailable();

                if (exportEnabled)
                {
                    metrics.AddOtlpExporter();
                }
            });

        return services;
    }

    private static MeterProviderBuilder AddRuntimeInstrumentationIfAvailable(this MeterProviderBuilder builder) =>
        builder.AddMeter("System.Runtime");
}
