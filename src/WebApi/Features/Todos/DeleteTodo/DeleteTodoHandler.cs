using Microsoft.EntityFrameworkCore;
using WebApi.Annotations;
using WebApi.Database;
using WebApi.Database.Models;

namespace WebApi.Features.Todos.DeleteTodo;

[Service(ServiceLifetime.Transient)]
internal sealed class DeleteTodoHandler(TodoContext context)
{
    public class Command
    {
        public TodoId Id { get; set; }
    }

    public class Result
    {
        public TodoId Id { get; set; }
    }

    public async Task<Result?> HandleAsync(Command request, CancellationToken ct)
    {
        var deleted = await context.Todos
            .Where(todo => todo.Id == request.Id)
            .ExecuteDeleteAsync(ct);

        if (deleted <= 0)
        {
            return null;
        }

        return new Result { Id = request.Id };
    }
}