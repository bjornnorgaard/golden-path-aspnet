using Microsoft.EntityFrameworkCore;
using WebApi.Annotations;
using WebApi.Database;
using WebApi.Telemetry;

namespace WebApi.Features.Todos.SweepOverdueTodos;

[Service(ServiceLifetime.Transient)]
internal sealed class SweepOverdueTodosHandler(TodoContext context, ILogger<SweepOverdueTodosHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var overdue = await context.Todos
            .AsNoTracking()
            .Where(t => !t.IsComplete)
            .Where(t => t.DueBy != null)
            .Where(t => t.DueBy < now)
            .ToListAsync(ct);

        foreach (var todo in overdue)
        {
            logger.LogWarning("Overdue: todo {TodoId} '{Title}' was due {DueBy}.", todo.Id, todo.Title, todo.DueBy);
            TelemetryConfig.RecordOverdueTodoDetected();
        }
    }
}
