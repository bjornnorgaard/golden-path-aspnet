using Microsoft.EntityFrameworkCore;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Platform.Annotations;
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
        return todo;
    }
}
