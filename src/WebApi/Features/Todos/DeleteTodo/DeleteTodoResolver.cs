using WebApi.Todos.Contracts;
using WebApi.Todos.GraphQl;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.DeleteTodo;

internal sealed class DeleteTodoResolver(DeleteTodoHandler handler) : IDeleteTodoResolver
{
    public async Task<DeleteTodoResponse> ResolveAsync(DeleteTodoRequest input, CancellationToken ct)
    {
        var todoId = TodoId.MustParse(input.Id);
        
        var deleted = await handler.HandleAsync(todoId, ct);
        if (!deleted)
        {
            throw new HotChocolate.GraphQLException("Todo was not found.");
        }

        return new DeleteTodoResponse
        {
            Id = todoId.Value
        };
    }
}
