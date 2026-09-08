using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace WebApi.Telemetry;

public static class TelemetryConfig
{
    private const string SourceName = "GoldenPath.WebApi";
    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(SourceName);

    private static readonly Counter<long> TodosCreatedCounter = Meter.CreateCounter<long>(
        name: "goldenpath.todos.created",
        unit: "{todo}",
        description: "Number of todos created.");

    private static readonly Counter<long> TodosCompletedCounter = Meter.CreateCounter<long>(
        name: "goldenpath.todos.completed",
        unit: "{todo}",
        description: "Number of todos completed.");

    public static void RecordTodoCreated() => TodosCreatedCounter.Add(1);

    public static void RecordTodoCompleted() => TodosCompletedCounter.Add(1);
}