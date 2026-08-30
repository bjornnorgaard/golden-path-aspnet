using WebApi.Todos.Contracts;
using WebApi.Todos.GraphQl;

namespace WebApi.Features.Todos.GetTodoList;

internal sealed class GetTodoListResolver(GetTodoListHandler handler) : IGetTodoListResolver
{
    public async Task<GetTodoListResponse> ResolveAsync(GetTodoListRequest input, CancellationToken ct)
    {
        var todos = await handler.HandleAsync(input.Limit, input.Offset, ct);
        
        return new GetTodoListResponse
        {
            Todos = todos
        };
    }
}
