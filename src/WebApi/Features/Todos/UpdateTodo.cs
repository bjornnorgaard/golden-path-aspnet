using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WebApi.Database;
using WebApi.Telemetry;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos;

public sealed class UpdateTodo(TodoContext context) : IUpdateTodoEndpoint
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

        var todo = await context.Todos.FirstOrDefaultAsync(t => t.Id == todoId, ct);
        if (todo is null)
        {
            return TypedResults.NotFound("Todo was not found.");
        }

        todo.Title = request.Title;
        todo.DueBy = request.DueBy?.UtcDateTime;
        todo.IsComplete = request.IsComplete;

        await context.SaveChangesAsync(ct);

        return TypedResults.Ok(new UpdateTodoResponse
        {
            Id = todo.Id.Value,
            Title = todo.Title,
            DueBy = todo.DueBy,
            IsComplete = todo.IsComplete
        });
    }
}