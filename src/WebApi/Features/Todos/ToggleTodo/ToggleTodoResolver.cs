using WebApi.Todos.Contracts;
using WebApi.Todos.GraphQl;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.ToggleTodo;

internal sealed class ToggleTodoResolver(ToggleTodoHandler handler) : IToggleTodoResolver
{
    public async Task<ToggleTodoResponse> ResolveAsync(ToggleTodoRequest input, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ToggleTodoHandler.Command
        {
            Id = TodoId.MustParse(input.Id)
        }, ct);
        if (result is null)
        {
            throw new HotChocolate.GraphQLException("Todo was not found.");
        }
        
        return new ToggleTodoResponse
        {
            Id = result.Id.Value,
            Title = result.Title,
            DueBy = result.DueBy,
            IsComplete = result.IsComplete
        };
    }
}
