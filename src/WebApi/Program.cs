using Microsoft.EntityFrameworkCore;
using WebApi.Platform;
using WebApi.Database;
using WebApi.Todos;

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddPlatform();
builder.AddWebApiGeneratedConfiguration();
builder.RegisterGeneratedServices();
builder.AddGeneratedTransportLayers();

var cs = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TodoContext>((_, opts) => opts.UseNpgsql(cs));

var app = builder.Build();
app.UsePlatform();
app.MapGeneratedTransportLayers();

app.Run();
