using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;

namespace WebApi.Features.Todos.GetTodoList;

internal sealed class GetTodoListEndpoint(GetTodoListHandler handler) : IGetTodoListEndpoint
{
    public async Task<Results<Ok<GetTodoListResponse>, BadRequest<string>>> HandleAsync(
        GetTodoListRequest request,
        CancellationToken ct)
    {
        var todos = await handler.HandleAsync(request.Page, request.PageSize, ct);

        return TypedResults.Ok(new GetTodoListResponse
        {
            Todos = todos
        });
    }
}
