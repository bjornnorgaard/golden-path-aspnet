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

    private static readonly Counter<long> TodoCompletionNotificationsSentCounter = Meter.CreateCounter<long>(
        name: "goldenpath.todos.completion_notifications_sent",
        unit: "{notification}",
        description: "Number of fire-and-forget completion notifications sent by Hangfire.");

    private static readonly Counter<long> TodoDueRemindersSentCounter = Meter.CreateCounter<long>(
        name: "goldenpath.todos.due_reminders_sent",
        unit: "{reminder}",
        description: "Number of delayed due-date reminders sent by Hangfire.");

    private static readonly Counter<long> OverdueTodosDetectedCounter = Meter.CreateCounter<long>(
        name: "goldenpath.todos.overdue_detected",
        unit: "{todo}",
        description: "Number of overdue todos detected by the recurring Hangfire sweep.");

    public static void RecordTodoCreated() => TodosCreatedCounter.Add(1);

    public static void RecordTodoCompleted() => TodosCompletedCounter.Add(1);

    public static void RecordTodoCompletionNotificationSent() => TodoCompletionNotificationsSentCounter.Add(1);

    public static void RecordTodoDueReminderSent() => TodoDueRemindersSentCounter.Add(1);

    public static void RecordOverdueTodoDetected() => OverdueTodosDetectedCounter.Add(1);
}