using WebApi.Todos.Contracts;
using WebApi.Todos.GraphQl;

namespace WebApi.Features.Todos.CreateTodo;

internal sealed class CreateTodoResolver(CreateTodoHandler handler) : ICreateTodoResolver
{
    public async Task<CreateTodoResponse> ResolveAsync(CreateTodoRequest input, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new CreateTodoHandler.Command
        {
            DueBy = input.DueBy?.Date,
            Title = input.Title
        }, ct);

        return new CreateTodoResponse
        {
            Id = result.Id,
            Title = input.Title,
            DueBy = input.DueBy?.Date,
            IsComplete = false
        };
    }
}
