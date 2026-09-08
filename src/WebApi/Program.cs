using Microsoft.EntityFrameworkCore;
using WebApi.Platform;
using WebApi.Database;

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddPlatform();

var cs = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TodoContext>((_, opts) => opts.UseNpgsql(cs));

var app = builder.Build();

using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<TodoContext>();
await dbContext.Database.MigrateAsync();

app.UsePlatform();

app.Run();
