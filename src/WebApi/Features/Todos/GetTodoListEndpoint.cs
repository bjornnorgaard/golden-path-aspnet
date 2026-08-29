using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;

namespace WebApi.Features.Todos;

internal sealed class GetTodoListEndpoint(GetTodoListHandler handler) : IGetTodoListEndpoint
{
    public async Task<Results<Ok<GetTodoListResponse>, BadRequest<string>>> HandleAsync(
        GetTodoListRequest request,
        CancellationToken ct)
    {
        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? 50;

        var todos = await handler.HandleAsync(page, pageSize, ct);

        return TypedResults.Ok(new GetTodoListResponse
        {
            Todos = todos
        });
    }
}
