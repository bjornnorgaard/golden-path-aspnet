using System.Diagnostics;
using WebApi.Telemetry;
using WebApi.Todos.Contracts;
using WebApi.Todos.GraphQl;

namespace WebApi.Features.Todos.CreateTodo;

internal sealed class CreateTodoResolver(CreateTodoHandler handler) : ICreateTodoResolver
{
    public async Task<CreateTodoResponse> ResolveAsync(CreateTodoRequest input, CancellationToken ct)
    {
        // Normalize to a UTC-kinded date once for both the command and the response.
        var dueBy = input.DueBy.ToUtcDueDate();

        var result = await handler.HandleAsync(new CreateTodoHandler.Command
        {
            DueBy = dueBy,
            Title = input.Title
        }, ct);

        Activity.Current?.SetTodoId(result.Id);

        return new CreateTodoResponse
        {
            Id = result.Id,
            Title = input.Title,
            DueBy = dueBy,
            IsComplete = false
        };
    }
}
