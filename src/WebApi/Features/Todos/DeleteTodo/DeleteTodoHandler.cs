using Microsoft.EntityFrameworkCore;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Platform.Annotations;

namespace WebApi.Features.Todos.DeleteTodo;

[Service(ServiceLifetime.Transient)]
internal sealed class DeleteTodoHandler(TodoContext context)
{
    public async Task<bool> HandleAsync(TodoId todoId, CancellationToken ct)
    {
        var deleted = await context.Todos
            .Where(todo => todo.Id == todoId)
            .ExecuteDeleteAsync(ct);

        return deleted > 0;
    }
}