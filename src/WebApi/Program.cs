using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WebApi.Database;
using WebApi.Database.Models;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddOpenApi();
builder.Services.RegisterGeneratedServices();

var cs = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TodoContext>(opts => opts.UseNpgsql(cs));

builder.Services.ConfigureHttpJsonOptions(opts => opts.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

var app = builder.Build();
app.MapOpenApi();

var todosApi = app.MapGroup("/todos");
todosApi.MapGet("/{id}", async Task<Results<Ok<Todo>, NotFound>> (string id, TodoContext db) =>
    {
        if (!TodoId.TryParse(id, out var parsedId))
        {
            return TypedResults.NotFound();
        }

        var todo = await db.Todos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == parsedId);
        if (todo == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(todo);
    })
    .WithName("GetTodoById");

app.Run();

[JsonSerializable(typeof(Todo[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}