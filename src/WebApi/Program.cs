using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Platform.Annotations;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

var app = builder.Build();
app.MapOpenApi();

Todo[] sampleTodos =
[
    new(TodoId.New(), "Walk the dog"),
    new(TodoId.New(), "Do the dishes", DateOnly.FromDateTime(DateTime.Now)),
    new(TodoId.New(), "Do the laundry", DateOnly.FromDateTime(DateTime.Now.AddDays(1))),
    new(TodoId.New(), "Clean the bathroom"),
    new(TodoId.New(), "Clean the car", DateOnly.FromDateTime(DateTime.Now.AddDays(2)))
];

var todosApi = app.MapGroup("/todos");
todosApi.MapGet("/", () => sampleTodos)
    .WithName("GetTodos");

todosApi.MapGet("/{id}", Results<Ok<Todo>, NotFound> (string id) =>
    {
        if (!TodoId.TryParse(id, out var parsedId))
        {
            return TypedResults.NotFound();
        }

        var todo = sampleTodos.FirstOrDefault(a => a.Id == parsedId);
        if (todo == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(todo);
    })
    .WithName("GetTodoById");

app.Run();

[StrongId]
public readonly partial struct TodoId;

public record Todo(TodoId Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

[JsonSerializable(typeof(Todo[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}