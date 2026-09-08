using Hangfire;
using WebApi.Annotations;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Todos.Contracts;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.CreateTodo;

[Service(ServiceLifetime.Transient)]
internal sealed class CreateTodoHandler(TodoContext context, IBackgroundJobClient backgroundJobClient)
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

        backgroundJobClient.Enqueue<TodoCreatedNotificationHandler>(
            handler => handler.HandleAsync(todo.Id.Value, todo.Title, CancellationToken.None));

        return todo;
    }
}
