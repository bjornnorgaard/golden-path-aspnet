using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Telemetry;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.UpdateTodo;

internal sealed class UpdateTodoEndpoint(UpdateTodoHandler handler) : IUpdateTodoEndpoint
{
    public async Task<Results<Ok<UpdateTodoResponse>, BadRequest<string>, NotFound<string>>> HandleAsync(
        UpdateTodoRequest request,
        CancellationToken ct)
    {
        if (!TodoId.TryParse(request.Id, out var todoId))
        {
            return TypedResults.BadRequest("Id must be a valid UUID.");
        }

        Activity.Current?.SetTodoId(todoId);

        var todo = await handler.HandleAsync(todoId, request, ct);
        if (todo is null)
        {
            return TypedResults.NotFound("Todo was not found.");
        }

        return TypedResults.Ok(new UpdateTodoResponse
        {
            Id = todo.Id.Value,
            Title = todo.Title,
            DueBy = todo.DueBy,
            IsComplete = todo.IsComplete
        });
    }
}
