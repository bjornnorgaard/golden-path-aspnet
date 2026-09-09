using Microsoft.EntityFrameworkCore;
using WebApi.Annotations;
using WebApi.Database;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.GetTodoById;

[Service(ServiceLifetime.Transient)]
internal sealed class GetTodoByIdHandler(TodoContext context)
{
    public class Command
    {
        public TodoId Id { get; set; }
    }

    public class Result
    {
        public TodoId Id { get; set; }
        public string Title { get; set; } = null!;
        public DateTime? DueBy { get; set; }
        public bool IsComplete { get; set; }
    }

    public Task<Result?> HandleAsync(Command request, CancellationToken ct)
    {
        return context.Todos
            .AsNoTracking()
            .Where(todo => todo.Id == request.Id)
            .Select(todo => new Result
            {
                Id = todo.Id,
                Title = todo.Title,
                DueBy = todo.DueBy,
                IsComplete = todo.IsComplete
            })
            .FirstOrDefaultAsync(ct);
    }
}
