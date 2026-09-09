using Microsoft.EntityFrameworkCore;
using WebApi.Annotations;
using WebApi.Database;
using WebApi.Telemetry;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.ToggleTodo;

[Service(ServiceLifetime.Transient)]
internal sealed class ToggleTodoHandler(TodoContext context)
{
    public class Command
    {
        public TodoId Id { get; set; }
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

        todo.IsComplete = !todo.IsComplete;
        await context.SaveChangesAsync(ct);

        if (todo.IsComplete)
        {
            TelemetryConfig.RecordTodoCompleted();
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
