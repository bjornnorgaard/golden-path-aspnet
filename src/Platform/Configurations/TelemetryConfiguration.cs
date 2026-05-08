using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            var endpoint = builder.Configuration.GetValue<string>("Telemetry:CollectorEndpoint");
            if (endpoint == null)
            {
                endpoint = "http://localhost:4317";
            }

            var collectorEndpoint = new Uri(endpoint, UriKind.Absolute);

            var resourceBuilder = ResourceBuilder
                .CreateDefault()
                .AddService(serviceName: builder.Environment.ApplicationName);

            builder.Services
                .AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(serviceName: builder.Environment.ApplicationName))
                .WithTracing(tracing => tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
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