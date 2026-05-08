using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Platform.Configurations;

public static class TelemetryConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformTelemetry()
        {
            var telemetry = builder.Configuration.GetTelemetry();
            var collectorEndpoint = new Uri(telemetry.CollectorEndpoint, UriKind.Absolute);

            var resourceBuilder = ResourceBuilder
                .CreateDefault()
                .AddService(serviceName: telemetry.ServiceName);

            builder.Services
                .AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(serviceName: telemetry.ServiceName))
                .WithTracing(tracing => tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddNpgsql()
                    .AddOtlpExporter(o => o.Endpoint = collectorEndpoint))
                .WithMetrics(metrics => metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = collectorEndpoint));

            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.SetResourceBuilder(resourceBuilder);
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
                logging.ParseStateValues = true;
                logging.AddOtlpExporter(o => o.Endpoint = collectorEndpoint);
            });
        }
    }

    extension(WebApplication app)
    {
        public void UsePlatformTelemetry()
        {
        }
    }
}