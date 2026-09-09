using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;

namespace WebApi.Features.Todos.GetTodoList;

internal sealed class GetTodoListEndpoint(GetTodoListHandler handler) : IGetTodoListEndpoint
{
    public async Task<Results<Ok<GetTodoListResponse>, BadRequest<IReadOnlyDictionary<string, string[]>>>> HandleAsync(
        GetTodoListRequest request,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetTodoListHandler.Command
        {
            Limit = request.Limit,
            Offset = request.Offset
        }, ct);

        return TypedResults.Ok(new GetTodoListResponse
        {
            Todos = result.Todos
        });
    }
}
