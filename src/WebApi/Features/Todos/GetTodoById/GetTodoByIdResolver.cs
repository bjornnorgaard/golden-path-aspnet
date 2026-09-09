using WebApi.Todos.Contracts;
using WebApi.Todos.GraphQl;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.GetTodoById;

internal sealed class GetTodoByIdResolver(GetTodoByIdHandler handler) : IGetTodoByIdResolver
{
    public async Task<GetTodoByIdResponse> ResolveAsync(GetTodoByIdRequest input, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetTodoByIdHandler.Command
        {
            Id = TodoId.MustParse(input.Id)
        }, ct);
        if (result is null)
        {
            throw new HotChocolate.GraphQLException("Todo was not found.");
        }

        return new GetTodoByIdResponse
        {
            Id = result.Id.Value,
            Title = result.Title,
            DueBy = result.DueBy,
            IsComplete = result.IsComplete
        };
    }
}
