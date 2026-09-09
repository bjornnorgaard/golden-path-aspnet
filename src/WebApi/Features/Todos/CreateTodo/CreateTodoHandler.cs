using WebApi.Annotations;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Telemetry;
using WebApi.Todos.Contracts;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.CreateTodo;

[Service(ServiceLifetime.Transient)]
internal sealed class CreateTodoHandler(TodoContext context)
{
    public class Command
    {
        public string Title { get; set; } = null!;
        public DateTime? DueBy { get; set; }
    }

    public class Result
    {
        public TodoId Id { get; set; }
    }
    
    public async Task<Result> HandleAsync(Command request, CancellationToken ct)
    {
        var todo = new Todo
        {
            Id = TodoId.New(),
            Title = request.Title,
            DueBy = request.DueBy,
            IsComplete = false
        };

        await context.Todos.AddAsync(todo, ct);
        await context.SaveChangesAsync(ct);

        TelemetryConfig.RecordTodoCreated();

        return new Result { Id = todo.Id };
    }
}