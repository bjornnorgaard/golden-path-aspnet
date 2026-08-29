using Microsoft.EntityFrameworkCore;
using WebApi.Platform.Annotations;
using WebApi.Database;
using TodoId = WebApi.Database.Models.TodoId;

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
