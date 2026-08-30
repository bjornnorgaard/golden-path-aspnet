using WebApi.Todos.Contracts;
using WebApi.Todos.GraphQl;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.UpdateTodo;

internal sealed class UpdateTodoResolver(UpdateTodoHandler handler) : IUpdateTodoResolver
{
    public async Task<UpdateTodoResponse> ResolveAsync(UpdateTodoRequest input, CancellationToken ct)
    {
        var todo = await handler.HandleAsync(TodoId.MustParse(input.Id), input, ct);
        if (todo is null)
        {
            throw new HotChocolate.GraphQLException("Todo was not found.");
        }
        
        return new UpdateTodoResponse
        {
            Id = todo.Id.Value,
            Title = todo.Title,
            DueBy = todo.DueBy,
            IsComplete = todo.IsComplete
        };
    }
}
