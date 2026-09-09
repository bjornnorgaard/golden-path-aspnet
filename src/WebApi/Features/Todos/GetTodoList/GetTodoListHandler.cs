using Microsoft.EntityFrameworkCore;
using WebApi.Annotations;
using WebApi.Database;
using WebApi.Configuration;
using WebApi.Todos.Contracts;

namespace WebApi.Features.Todos.GetTodoList;

[Service(ServiceLifetime.Transient)]
internal sealed class GetTodoListHandler(TodoContext context, PagingOptions paging)
{
    public class Command
    {
        public int? Limit { get; set; }
        public int? Offset { get; set; }
    }

    public class Result
    {
        public GetTodoListItem[] Todos { get; set; } = [];
    }

    public async Task<Result> HandleAsync(Command request, CancellationToken ct)
    {
        var effectiveLimit = Math.Min(request.Limit ?? paging.DefaultPageSize, paging.MaxPageSize);
        var effectiveOffset = request.Offset ?? 0;

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
            .ToArrayAsync(ct);

        return new Result { Todos = todos };
    }
}
