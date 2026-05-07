using Microsoft.EntityFrameworkCore;
using Platform.Annotations;
using WebApi.Database;
using WebApi.Database.Models;

namespace WebApi.features.todos;

public class GetTodo
{
    public class Request
    {
        public required TodoId Id { get; init; }
    }

    public class Result
    {
        public required TodoId Id { get; init; }
        public required string Title { get; init; }
        public DateTime? DueBy { get; init; }
        public bool IsComplete { get; init; }
    }

    [Service(lifetime: ServiceLifetime.Transient, asSelf: true)]
    public class Handler(TodoContext context)
    {
        public async Task<Result?> Handle(Request req, CancellationToken ct)
        {
            var todo = await context.Todos.FirstOrDefaultAsync(t => t.Id == req.Id, ct);
            if (todo == null) return null;

            return new Result
            {
                Id = todo.Id,
                Title = todo.Title,
                DueBy = todo.DueBy,
                IsComplete = todo.IsComplete
            };
        }
    }
}