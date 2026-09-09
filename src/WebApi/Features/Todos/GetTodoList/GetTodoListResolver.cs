using HotChocolate.Execution.Processing;
using HotChocolate.Resolvers;
using Microsoft.EntityFrameworkCore;
using WebApi.Configuration;
using WebApi.Database;
using WebApi.Todos.Contracts;
using WebApi.Todos.GraphQl;

namespace WebApi.Features.Todos.GetTodoList;

internal sealed class GetTodoListResolver(TodoContext context, PagingOptions paging) : IGetTodoListResolver
{
    public async Task<GetTodoListResponse> ResolveAsync(GetTodoListRequest input, IResolverContext resolverContext, CancellationToken ct)
    {
        var effectiveLimit = Math.Min(input.Limit ?? paging.DefaultPageSize, paging.MaxPageSize);
        var effectiveOffset = input.Offset ?? 0;

        var todosSelections = resolverContext.Select("todos");
        if (todosSelections.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one selection for the 'todos' field but found {todosSelections.Count}.");
        }

        var todosSelection = (Selection)todosSelections[0];

        var todos = await context.Todos
            .AsNoTracking()
            .OrderBy(todo => todo.Id)
            .Skip(effectiveOffset)
            .Take(effectiveLimit)
            .Select(todo => new GetTodoListItem
            {
                Id = todo.Id.Value,
                Title = todo.Title,
                DueBy = todo.DueBy,
                IsComplete = todo.IsComplete
            })
            .Select(todosSelection)
            .ToArrayAsync(ct);

        return new GetTodoListResponse
        {
            Todos = todos
        };
    }
}