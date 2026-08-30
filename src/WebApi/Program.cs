using Microsoft.EntityFrameworkCore;
using WebApi.Platform;
using WebApi.Database;
using WebApi.Todos;
using WebApi.Todos.GraphQl;

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddPlatform();
builder.AddWebApiGeneratedConfiguration();
builder.RegisterGeneratedServices();
builder.AddGeneratedTransportLayers();

var cs = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TodoContext>((_, opts) => opts.UseNpgsql(cs));

var app = builder.Build();

using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<TodoContext>();
await dbContext.Database.MigrateAsync();

app.UsePlatform();
app.MapGeneratedTransportLayers();
app.MapGeneratedGraphQlPlayground();

app.Run();
