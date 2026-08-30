using Microsoft.EntityFrameworkCore;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Platform.Annotations;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.GetTodoById;

[Service(ServiceLifetime.Transient)]
internal sealed class GetTodoByIdHandler(TodoContext context)
{
    public Task<Todo?> HandleAsync(TodoId todoId, CancellationToken ct)
    {
        return context.Todos.AsNoTracking().FirstOrDefaultAsync(todo => todo.Id == todoId, ct);
    }
}
