using System.Diagnostics;
using HotChocolate.Resolvers;
using Microsoft.EntityFrameworkCore;
using WebApi.Database;
using WebApi.Telemetry;
using WebApi.Todos.Contracts;
using WebApi.Todos.GraphQl;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.GetTodoById;

internal sealed class GetTodoByIdResolver(TodoContext context) : IGetTodoByIdResolver
{
    public async Task<GetTodoByIdResponse> ResolveAsync(GetTodoByIdRequest input, IResolverContext resolverContext, CancellationToken ct)
    {
        var id = TodoId.MustParse(input.Id);
        Activity.Current?.SetTodoId(id);

        var result = await context.Todos
            .AsNoTracking()
            .Where(todo => todo.Id == id)
            .Select(todo => new GetTodoByIdResponse
            {
                Id = todo.Id.Value,
                Title = todo.Title,
                DueBy = todo.DueBy,
                IsComplete = todo.IsComplete
            })
            .Select(resolverContext.Selection)
            .FirstOrDefaultAsync(ct);
        
        if (result is null)
        {
            throw new HotChocolate.GraphQLException("Todo was not found.");
        }

        return result;
    }
}
