using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using WebApi.Annotations;
using WebApi.Database;
using WebApi.Telemetry;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.SendTodoDueReminder;

[Service(ServiceLifetime.Transient)]
internal sealed class SendTodoDueReminderHandler(TodoContext context, ILogger<SendTodoDueReminderHandler> logger)
{
    public class Command
    {
        public TodoId TodoId { get; set; }
    }

    public async Task HandleAsync(Command request, CancellationToken ct)
    {
        Activity.Current?.SetTodoId(request.TodoId);

        var todo = await context.Todos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TodoId, ct);
        if (todo is null)
        {
            logger.LogDebug("Skipping due reminder for todo {TodoId}: it no longer exists.", request.TodoId);
            return;
        }

        if (todo.IsComplete)
        {
            logger.LogDebug("Skipping due reminder for todo {TodoId}: it was already completed.", todo.Id);
            return;
        }

        logger.LogWarning("Reminder: todo {TodoId} '{Title}' is now due.", todo.Id, todo.Title);
        TelemetryConfig.RecordTodoDueReminderSent();
    }
}
