using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Telemetry;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.DeleteTodo;

internal sealed class DeleteTodoEndpoint(DeleteTodoHandler handler) : IDeleteTodoEndpoint
{
    public async Task<Results<Ok<DeleteTodoResponse>, BadRequest<IReadOnlyDictionary<string, string[]>>, NotFound<string>>> HandleAsync(
        DeleteTodoRequest request,
        CancellationToken ct)
    {
        if (!TodoId.TryParse(request.Id, out var todoId))
        {
            return TypedResults.BadRequestTodoIdInvalid();
        }
        
        Activity.Current?.SetTodoId(todoId);

        var result = await handler.HandleAsync(new DeleteTodoHandler.Command { Id = todoId }, ct);
        if (result is null)
        {
            return TypedResults.NotFound("Todo was not found.");
        }

        return TypedResults.Ok(new DeleteTodoResponse { Id = result.Id.Value });
    }
}
