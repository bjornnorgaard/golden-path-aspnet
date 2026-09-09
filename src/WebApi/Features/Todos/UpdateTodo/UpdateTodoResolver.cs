using WebApi.Todos.Contracts;
using WebApi.Todos.GraphQl;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.UpdateTodo;

internal sealed class UpdateTodoResolver(UpdateTodoHandler handler) : IUpdateTodoResolver
{
    public async Task<UpdateTodoResponse> ResolveAsync(UpdateTodoRequest input, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new UpdateTodoHandler.Command
        {
            Id = TodoId.MustParse(input.Id),
            Title = input.Title,
            DueBy = input.DueBy?.Date,
            IsComplete = input.IsComplete
        }, ct);
        if (result is null)
        {
            throw new HotChocolate.GraphQLException("Todo was not found.");
        }
        
        return new UpdateTodoResponse
        {
            Id = result.Id.Value,
            Title = result.Title,
            DueBy = result.DueBy,
            IsComplete = result.IsComplete
        };
    }
}
