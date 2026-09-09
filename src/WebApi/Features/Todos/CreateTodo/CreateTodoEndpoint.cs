using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Telemetry;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;

namespace WebApi.Features.Todos.CreateTodo;

internal sealed class CreateTodoEndpoint(CreateTodoHandler handler) : ICreateTodoEndpoint
{
    public async Task<Results<Ok<CreateTodoResponse>, BadRequest<IReadOnlyDictionary<string, string[]>>>> HandleAsync(
        CreateTodoRequest request,
        CancellationToken ct)
    {
        // Normalize to a UTC-kinded value once for both the command and the response.
        var dueBy = request.DueBy.ToUtcDueBy();

        var result = await handler.HandleAsync(new CreateTodoHandler.Command
        {
            Title = request.Title,
            DueBy = dueBy
        }, ct);

        Activity.Current?.SetTodoId(result.Id);

        return TypedResults.Ok(new CreateTodoResponse
        {
            Id = result.Id,
            Title = request.Title,
            DueBy = dueBy,
            IsComplete = false
        });
    }
}
