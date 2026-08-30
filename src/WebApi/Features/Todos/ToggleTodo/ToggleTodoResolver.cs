using WebApi.Todos.Contracts;
using WebApi.Todos.GraphQl;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.ToggleTodo;

internal sealed class ToggleTodoResolver(ToggleTodoHandler handler) : IToggleTodoResolver
{
    public async Task<ToggleTodoResponse> ResolveAsync(ToggleTodoRequest input, CancellationToken ct)
    {
        var todo = await handler.HandleAsync(TodoId.MustParse(input.Id), ct);
        if (todo is null)
        {
            throw new HotChocolate.GraphQLException("Todo was not found.");
        }
        return new ToggleTodoResponse
        {
            Id = todo.Id.Value,
            Title = todo.Title,
            DueBy = todo.DueBy,
            IsComplete = todo.IsComplete
        };
    }
}
