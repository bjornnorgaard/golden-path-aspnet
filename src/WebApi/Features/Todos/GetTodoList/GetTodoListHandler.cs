using Microsoft.EntityFrameworkCore;
using WebApi.Platform.Annotations;
using WebApi.Database;
using WebApi.Configuration;
using WebApi.Todos.Contracts;

namespace WebApi.Features.Todos.GetTodoList;

[Service(ServiceLifetime.Transient)]
internal sealed class GetTodoListHandler(TodoContext context, PagingOptions paging)
{
    public Task<GetTodoListItem[]> HandleAsync(int? page, int? pageSize, CancellationToken ct)
    {
        var effectivePage = Math.Max(page ?? 1, 1);
        var effectivePageSize = Math.Min(pageSize ?? paging.DefaultPageSize, paging.MaxPageSize);

        return context.Todos
            .AsNoTracking()
            .OrderBy(todo => todo.Id)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
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
