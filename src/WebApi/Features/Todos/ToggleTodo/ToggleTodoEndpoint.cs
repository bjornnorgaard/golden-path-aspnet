using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Telemetry;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.ToggleTodo;

internal sealed class ToggleTodoEndpoint(ToggleTodoHandler handler) : IToggleTodoEndpoint
{
    public async Task<Results<Ok<ToggleTodoResponse>, BadRequest<IReadOnlyDictionary<string, string[]>>, NotFound<string>>> HandleAsync(
        ToggleTodoRequest request,
        CancellationToken ct)
    {
        if (!TodoId.TryParse(request.Id, out var todoId))
        {
            return TypedResults.BadRequestTodoIdInvalid();
        }

        Activity.Current?.SetTodoId(todoId);

        var result = await handler.HandleAsync(new ToggleTodoHandler.Command { Id = todoId }, ct);
        if (result is null)
        {
            return TypedResults.NotFound("Todo was not found.");
        }

        return TypedResults.Ok(new ToggleTodoResponse
        {
            Id = result.Id.Value,
            Title = result.Title,
            DueBy = result.DueBy,
            IsComplete = result.IsComplete
        });
    }
}
