using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using WebApi.Annotations;
using WebApi.Database;
using WebApi.Telemetry;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.SendTodoCompletedNotification;

[Service(ServiceLifetime.Transient)]
internal sealed class SendTodoCompletedNotificationHandler(TodoContext context, ILogger<SendTodoCompletedNotificationHandler> logger)
{
    public class Command
    {
        public TodoId TodoId { get; set; }
    }

    public async Task HandleAsync(Command request, CancellationToken ct)
    {
        Activity.Current?.SetTodoId(request.TodoId);

        var todo = await context.Todos
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TodoId, ct);
            
        if (todo is null)
        {
            return;
        }

        logger.LogInformation("Notification: todo {TodoId} '{Title}' was completed.", todo.Id, todo.Title);
        TelemetryConfig.RecordTodoCompletionNotificationSent();
    }
}
