using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace WebApi.Platform.Configurations;

public static class TelemetryConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformTelemetry()
        {
            var collectorEndpoint = builder.Configuration["Telemetry:CollectorEndpoint"];
            if (collectorEndpoint == null)
            {
                throw new InvalidOperationException("Required configuration value is missing: Telemetry:CollectorEndpoint");
            }

            var serviceName = builder.Configuration["Telemetry:ServiceName"];
            if (serviceName == null)
            {
                throw new InvalidOperationException("Required configuration value is missing: Telemetry:ServiceName");
            }

            var endpoint = new Uri(collectorEndpoint, UriKind.Absolute);

            var resourceBuilder = ResourceBuilder
                .CreateDefault()
                .AddService(serviceName: serviceName);

            builder.Services
                .AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(serviceName: serviceName))
                .WithTracing(tracing => tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddNpgsql()
                    .AddOtlpExporter(o => o.Endpoint = endpoint))
                .WithMetrics(metrics => metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = endpoint));

            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.SetResourceBuilder(resourceBuilder);
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
                logging.ParseStateValues = true;
                logging.AddOtlpExporter(o => o.Endpoint = endpoint);
            });
        }
    }
}
