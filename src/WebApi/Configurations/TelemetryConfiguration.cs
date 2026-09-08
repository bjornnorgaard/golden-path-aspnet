using System.Diagnostics;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using WebApi.Telemetry;

namespace WebApi.Configurations;

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
                    .AddSource(TelemetryConfig.ActivitySource.Name)
                    .AddProcessor(new GraphQlOperationNameProcessor())
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddHangfireInstrumentation()
                    .AddNpgsql()
                    .AddOtlpExporter(o => o.Endpoint = endpoint))
                .WithMetrics(metrics => metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(TelemetryConfig.Meter.Name)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddHangfireInstrumentation()
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

    private sealed class GraphQlOperationNameProcessor : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity activity)
        {
            if (activity.Kind == ActivityKind.Server && activity.GetTagItem("graphql.operation.name") is string operationName)
            {
                activity.DisplayName = operationName;
            }
        }
    }
}