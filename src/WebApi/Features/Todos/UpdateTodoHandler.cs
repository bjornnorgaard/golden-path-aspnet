using Microsoft.EntityFrameworkCore;
using Platform.Annotations;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Todos.Contracts;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos;

[Service(ServiceLifetime.Transient)]
internal sealed class UpdateTodoHandler(TodoContext context)
{
    public async Task<Todo?> HandleAsync(TodoId todoId, UpdateTodoRequest request, CancellationToken ct)
    {
        var todo = await context.Todos.FirstOrDefaultAsync(item => item.Id == todoId, ct);

        if (todo is null)
        {
            return null;
        }

        todo.Title = request.Title;
        todo.DueBy = request.DueBy?.UtcDateTime;
        todo.IsComplete = request.IsComplete;
        await context.SaveChangesAsync(ct);
        return todo;
    }
}
