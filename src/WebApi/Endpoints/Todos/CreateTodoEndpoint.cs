using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using WebApi.Features.Todos;

namespace WebApi.Endpoints.Todos;

public static class CreateTodoEndpoint
{
    public static async Task<Results<Created<GetTodo.Result>, BadRequest>> Handle(
        [FromBody] CreateTodo.Request req,
        CreateTodo.Handler handler,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
        {
            return TypedResults.BadRequest();
        }

        var created = await handler.Handle(req, ct);

        // Avoid relying on StrongId JSON shape; Location must be a plain string route.
        var location = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{Routes.Todos.Create}/{created.Id.Value}";
        return TypedResults.Created(location, new GetTodo.Result
        {
            Id = created.Id,
            Title = created.Title,
            DueBy = created.DueBy,
            IsComplete = created.IsComplete
        });
    }
}