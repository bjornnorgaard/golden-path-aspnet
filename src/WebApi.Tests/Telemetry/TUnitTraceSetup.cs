using System.Diagnostics;
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
            .AddProcessor(new TestCaseNameProcessor())
            .AddOtlpExporter(opts => opts.Endpoint = CollectorEndpoint)
            .Build();
    }

    private sealed class TestCaseNameProcessor : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity activity)
        {
            // TUnit uses "test case" as the operation name; rename it to something searchable in UIs.
            // The tag name follows OpenTelemetry test semantic conventions.
            if (!string.Equals(activity.DisplayName, "test case", StringComparison.OrdinalIgnoreCase))
            {
                // These are not the test cases we're looking for
                return;
            }

            var testCaseName = activity.GetTagItem("test.case.name") as string;
            if (!string.IsNullOrWhiteSpace(testCaseName))
            {
                activity.DisplayName = testCaseName;
            }
        }
    }

    [After(TestSession)]
    public static void TeardownTracing()
    {
        _tracerProvider?.Dispose();
    }
}