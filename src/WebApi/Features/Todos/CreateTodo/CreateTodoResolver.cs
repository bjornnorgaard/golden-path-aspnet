using WebApi.Todos.Contracts;
using WebApi.Todos.GraphQl;

namespace WebApi.Features.Todos.CreateTodo;

internal sealed class CreateTodoResolver(CreateTodoHandler handler) : ICreateTodoResolver
{
    public async Task<CreateTodoResponse> ResolveAsync(CreateTodoRequest input, CancellationToken ct)
    {
        var todo = await handler.HandleAsync(input, ct);

        return new CreateTodoResponse
        {
            Id = todo.Id,
            Title = todo.Title,
            DueBy = todo.DueBy,
            IsComplete = todo.IsComplete
        };
    }
}
