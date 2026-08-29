using Platform.Annotations;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Todos.Contracts;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos;

[Service(ServiceLifetime.Transient)]
internal sealed class CreateTodoHandler(TodoContext context)
{
    public async Task<Todo> HandleAsync(CreateTodoRequest request, CancellationToken ct)
    {
        var todo = new Todo
        {
            Id = TodoId.New(),
            Title = request.Title,
            DueBy = request.DueBy?.UtcDateTime,
            IsComplete = false
        };

        await context.Todos.AddAsync(todo, ct);
        await context.SaveChangesAsync(ct);
        return todo;
    }
}
