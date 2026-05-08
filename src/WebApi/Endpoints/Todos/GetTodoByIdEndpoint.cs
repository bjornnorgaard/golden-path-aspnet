using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using WebApi.Database.Models;
using WebApi.Features.Todos;

namespace WebApi.Endpoints.Todos;

public static class GetTodoByIdEndpoint
{
    public class Request
    {
        public required string Id { get; init; }
    }

    public static async Task<Results<Ok<GetTodo.Result>, NotFound>> Handle(
        [FromBody] Request req,
        GetTodo.Handler handler,
        CancellationToken ct)
    {
        if (!TodoId.TryParse(req.Id, out var parsedId))
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