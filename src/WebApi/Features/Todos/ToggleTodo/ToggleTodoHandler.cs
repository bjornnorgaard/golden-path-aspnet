using Microsoft.EntityFrameworkCore;
using WebApi.Annotations;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Telemetry;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.ToggleTodo;

[Service(ServiceLifetime.Transient)]
internal sealed class ToggleTodoHandler(TodoContext context)
{
    public async Task<Todo?> HandleAsync(TodoId todoId, CancellationToken ct)
    {
        var todo = await context.Todos.FirstOrDefaultAsync(item => item.Id == todoId, ct);

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

        return todo;
    }
}
