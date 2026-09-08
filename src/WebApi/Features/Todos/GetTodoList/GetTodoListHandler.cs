using Microsoft.EntityFrameworkCore;
using WebApi.Annotations;
using WebApi.Database;
using WebApi.Configuration;
using WebApi.Todos.Contracts;

namespace WebApi.Features.Todos.GetTodoList;

[Service(ServiceLifetime.Transient)]
internal sealed class GetTodoListHandler(TodoContext context, PagingOptions paging)
{
    public Task<GetTodoListItem[]> HandleAsync(int? limit, int? offset, CancellationToken ct)
    {
        var effectiveLimit = Math.Min(limit ?? paging.DefaultPageSize, paging.MaxPageSize);
        var effectiveOffset = offset ?? 0;

        return context.Todos
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
            .ToArrayAsync(ct);
    }
}
