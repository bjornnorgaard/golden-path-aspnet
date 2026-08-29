using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Telemetry;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos;

internal sealed class DeleteTodoEndpoint(DeleteTodoHandler handler) : IDeleteTodoEndpoint
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

        if (!await handler.HandleAsync(todoId, ct))
        {
            return TypedResults.NotFound("Todo was not found.");
        }

        return TypedResults.Ok(new DeleteTodoResponse { Id = todoId.Value });
    }
}
