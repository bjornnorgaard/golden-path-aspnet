using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WebApi.Database;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;

namespace WebApi.Features.Todos;

public sealed class GetTodoList(TodoContext context) : IGetTodoListEndpoint
{
    public async Task<Results<Ok<GetTodoListResponse>, BadRequest<string>>> HandleAsync(
        GetTodoListRequest request,
        CancellationToken ct)
    {
        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? 50;

        var todos = await context.Todos
            .AsNoTracking()
            .OrderBy(todo => todo.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(todo => new GetTodoListItem
            {
                Id = todo.Id.Value,
                Title = todo.Title,
                DueBy = todo.DueBy,
                IsComplete = todo.IsComplete
            })
            .ToArrayAsync(ct);

        return TypedResults.Ok(new GetTodoListResponse
        {
            Todos = todos
        });
    }
}
