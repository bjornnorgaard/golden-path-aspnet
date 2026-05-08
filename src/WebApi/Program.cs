using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using WebApi;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Endpoints;
using WebApi.Json;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddOpenApi();
builder.Services.RegisterGeneratedServices();

builder.Services.AddDbContext<TodoContext>((sp, opts) =>
{
    var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
    opts.UseNpgsql(cs);
});
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.Converters.Add(new TodoIdJsonConverter());
    opts.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var app = builder.Build();
app.MapOpenApi();
app.MapTodos();

app.Run();

namespace WebApi
{
    [JsonSerializable(typeof(Todo[]))]
    internal partial class AppJsonSerializerContext : JsonSerializerContext
    {
    }
}