using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WebApi.Database;
using WebApi.Telemetry;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos;

public sealed class DeleteTodo(TodoContext context) : IDeleteTodoEndpoint
{
    public async Task<Results<Ok<DeleteTodoResponse>, BadRequest<string>, NotFound<string>>> HandleAsync(
        DeleteTodoRequest request,
        CancellationToken ct)
    {
        if (!TodoId.TryParse(request.Id, out var todoId))
        {
            return TypedResults.BadRequest("Id must be a valid UUID.");
        }
        
        Activity.Current?.SetTodoId(todoId);

        var deleted = await context.Todos
            .Where(todo => todo.Id == todoId)
            .ExecuteDeleteAsync(ct);
        
        if (deleted == 0)
        {
            return TypedResults.NotFound("Todo was not found.");
        }

        return TypedResults.Ok(new DeleteTodoResponse { Id = todoId.Value });
    }
}
