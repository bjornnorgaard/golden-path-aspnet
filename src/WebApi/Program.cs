using Microsoft.EntityFrameworkCore;
using WebApi.Platform;
using WebApi.Database;
using WebApi.Todos.Endpoints;

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddPlatform();
builder.AddWebApiGeneratedConfiguration();
builder.RegisterGeneratedServices();
builder.Services.AddGeneratedOpenApiEndpoints();

var cs = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TodoContext>((_, opts) => opts.UseNpgsql(cs));

var app = builder.Build();
app.UsePlatform();
app.MapGeneratedOpenApiEndpoints();

app.Run();
