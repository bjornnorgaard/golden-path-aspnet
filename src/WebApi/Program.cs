using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Endpoints;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddOpenApi();
builder.Services.RegisterGeneratedServices();

var cs = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TodoContext>(opts => opts.UseNpgsql(cs));

builder.Services.ConfigureHttpJsonOptions(opts => opts.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

var app = builder.Build();
app.MapOpenApi();
app.MapTodos();

app.Run();

[JsonSerializable(typeof(Todo[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}