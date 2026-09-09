using Hangfire;
using Microsoft.EntityFrameworkCore;
using WebApi.Annotations;
using WebApi.Database;
using WebApi.Features.Todos.SendTodoCompletedNotification;
using WebApi.Telemetry;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.UpdateTodo;

[Service(ServiceLifetime.Transient)]
internal sealed class UpdateTodoHandler(TodoContext context, IBackgroundJobClient jobs)
{
    public class Command
    {
        public TodoId Id { get; set; }
        public string Title { get; set; } = null!;
        public DateTime? DueBy { get; set; }
        public bool IsComplete { get; set; }
    }

    public class Result
    {
        public TodoId Id { get; set; }
        public string Title { get; set; } = null!;
        public DateTime? DueBy { get; set; }
        public bool IsComplete { get; set; }
    }

    public async Task<Result?> HandleAsync(Command request, CancellationToken ct)
    {
        var todo = await context.Todos.FirstOrDefaultAsync(item => item.Id == request.Id, ct);

        if (todo is null)
        {
            return null;
        }

        var wasComplete = todo.IsComplete;

        todo.Title = request.Title;
        todo.DueBy = request.DueBy;
        todo.IsComplete = request.IsComplete;
        await context.SaveChangesAsync(ct);

        if (todo.IsComplete && !wasComplete)
        {
            TelemetryConfig.RecordTodoCompleted();

            // Fire-and-forget: the caller doesn't need to wait for this to run.
            jobs.Enqueue<SendTodoCompletedNotificationHandler>(j => j.HandleAsync(new SendTodoCompletedNotificationHandler.Command { TodoId = todo.Id }, CancellationToken.None));
        }

        return new Result
        {
            Id = todo.Id,
            Title = todo.Title,
            DueBy = todo.DueBy,
            IsComplete = todo.IsComplete
        };
    }
}
