using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Database.Models;
using WebApi.features.todos;

namespace WebApi.Endpoints.Todos;

public static class GetTodoByIdEndpoint
{
    public static async Task<Results<Ok<GetTodo.Result>, NotFound>> Handle(
        string id,
        GetTodo.Handler handler,
        CancellationToken ct)
    {
        if (!TodoId.TryParse(id, out var parsedId))
        {
            return TypedResults.NotFound();
        }

        var todo = await handler.Handle(new GetTodo.Request { Id = parsedId }, ct);
        if (todo == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(todo);
    }
}