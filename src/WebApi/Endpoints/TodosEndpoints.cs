using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Database.Models;
using WebApi.features.todos;

namespace WebApi.Endpoints;

public static class TodosEndpoints
{
    public static IEndpointRouteBuilder MapTodos(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/todos");
        group.MapGet("/{id}", GetTodoById)
            .WithName("GetTodoById");
        return app;
    }

    private static async Task<Results<
            Ok<GetTodo.Result>,
            NotFound>
    > GetTodoById(string id, GetTodo.Handler handler, CancellationToken ct)
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