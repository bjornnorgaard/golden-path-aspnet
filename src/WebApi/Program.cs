using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platform;
using WebApi;
using WebApi.Database;
using WebApi.Json;

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddPlatform();
builder.Services.RegisterGeneratedServices();
builder.Services.AddValidatorsFromAssemblyContaining<AssemblyAnchor>();

var cs = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TodoContext>((_, opts) => opts.UseNpgsql(cs));
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.Converters.Add(new TodoIdJsonConverter());
    opts.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var app = builder.Build();
app.UsePlatform();
app.MapGeneratedEndpoints();

app.Run();