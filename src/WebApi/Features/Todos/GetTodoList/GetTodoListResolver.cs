using WebApi.Todos.Contracts;
using WebApi.Todos.GraphQl;

namespace WebApi.Features.Todos.GetTodoList;

internal sealed class GetTodoListResolver(GetTodoListHandler handler) : IGetTodoListResolver
{
    public async Task<GetTodoListResponse> ResolveAsync(GetTodoListRequest input, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetTodoListHandler.Command
        {
            Limit = input.Limit,
            Offset = input.Offset
        }, ct);
        
        return new GetTodoListResponse
        {
            Todos = result.Todos
        };
    }
}
