using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace WebApi.Tests.Telemetry;

// ReSharper disable once InconsistentNaming
public static class TUnitTraceSetup
{
    private static TracerProvider? _tracerProvider;
    private static readonly Uri CollectorEndpoint = new("http://localhost:4317", UriKind.Absolute);

    [Before(TestDiscovery)]
    public static void SetupTracing()
    {
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService("tunit");

        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource("TUnit")
            .AddSource("TUnit.Lifecycle")
            .AddOtlpExporter(opts => opts.Endpoint = CollectorEndpoint)
            .Build();
    }

    [After(TestSession)]
    public static void TeardownTracing()
    {
        _tracerProvider?.Dispose();
    }
}