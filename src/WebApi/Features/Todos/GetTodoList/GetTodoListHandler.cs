using Microsoft.EntityFrameworkCore;
using Platform.Annotations;
using WebApi.Database;
using WebApi.Todos.Contracts;

namespace WebApi.Features.Todos.GetTodoList;

[Service(ServiceLifetime.Transient)]
internal sealed class GetTodoListHandler(TodoContext context)
{
    public Task<GetTodoListItem[]> HandleAsync(int page, int pageSize, CancellationToken ct)
    {
        return context.Todos
            .AsNoTracking()
            .OrderBy(todo => todo.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
