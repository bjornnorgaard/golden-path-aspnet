using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Telemetry;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;

namespace WebApi.Features.Todos.CreateTodo;

internal sealed class CreateTodoEndpoint(CreateTodoHandler handler) : ICreateTodoEndpoint
{
    public async Task<Results<Ok<CreateTodoResponse>, BadRequest<string>>> HandleAsync(
        CreateTodoRequest request,
        CancellationToken ct)
    {
        var todo = await handler.HandleAsync(request, ct);

        Activity.Current?.SetTodoId(todo.Id);

        return TypedResults.Ok(new CreateTodoResponse
        {
            Id = todo.Id,
            Title = todo.Title,
            DueBy = todo.DueBy,
            IsComplete = todo.IsComplete
        });
    }
}
