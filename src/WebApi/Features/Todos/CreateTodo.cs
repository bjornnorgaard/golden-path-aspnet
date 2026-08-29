using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Telemetry;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos;

public sealed class CreateTodo(TodoContext context) : ICreateTodoEndpoint
{
    public async Task<Results<Ok<CreateTodoResponse>, BadRequest<string>>> HandleAsync(
        CreateTodoRequest request,
        CancellationToken ct)
    {
        var dbTodo = new Todo
        {
            Id = TodoId.New(),
            Title = request.Title,
            DueBy = request.DueBy?.UtcDateTime,
            IsComplete = false
        };

        Activity.Current?.SetTodoId(dbTodo.Id);
        await context.Todos.AddAsync(dbTodo, ct);
        await context.SaveChangesAsync(ct);

        return TypedResults.Ok(new CreateTodoResponse
        {
            Id = dbTodo.Id,
            Title = dbTodo.Title,
            DueBy = dbTodo.DueBy,
            IsComplete = dbTodo.IsComplete
        });
    }
}
