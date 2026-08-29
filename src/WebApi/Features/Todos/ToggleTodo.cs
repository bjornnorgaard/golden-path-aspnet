using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WebApi.Database;
using WebApi.Telemetry;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos;

public sealed class ToggleTodo(TodoContext context) : IToggleTodoEndpoint
{
    public async Task<Results<Ok<ToggleTodoResponse>, BadRequest<string>, NotFound<string>>> HandleAsync(
        ToggleTodoRequest request,
        CancellationToken ct)
    {
        if (!TodoId.TryParse(request.Id, out var todoId))
        {
            return TypedResults.BadRequest("Id must be a valid UUID.");
        }

        Activity.Current?.SetTodoId(todoId);

        var todo = await context.Todos.FirstOrDefaultAsync(item => item.Id == todoId, ct);
        if (todo is null)
        {
            return TypedResults.NotFound("Todo was not found.");
        }

        todo.IsComplete = !todo.IsComplete;
        await context.SaveChangesAsync(ct);
        
        return TypedResults.Ok(new ToggleTodoResponse
        {
            Id = todo.Id.Value,
            Title = todo.Title,
            DueBy = todo.DueBy,
            IsComplete = todo.IsComplete
        });
    }
}