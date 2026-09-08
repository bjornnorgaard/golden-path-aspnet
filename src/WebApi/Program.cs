using WebApi.Platform.Configurations;
using WebApi.Todos;
using WebApi.Todos.GraphQl;

var builder = WebApplication.CreateSlimBuilder(args);
builder.AddPlatformExceptionHandling();
builder.AddPlatformTelemetry();
builder.AddWebApiGeneratedConfiguration();
builder.RegisterGeneratedServices();
builder.AddGeneratedTransportLayers();
builder.AddDatabase();

var app = builder.Build();
app.UsePlatformExceptionHandling();
app.MapPlatformOpenApi();
app.MapGeneratedTransportLayers();
app.MapGeneratedGraphQlPlayground();
app.UseDatabase();

app.Run();