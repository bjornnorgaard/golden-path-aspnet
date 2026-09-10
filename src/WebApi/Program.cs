using WebApi.Configurations;
using WebApi.Todos;
using WebApi.Todos.GraphQl;

var builder = WebApplication.CreateSlimBuilder(args);
builder.AddPlatformExceptionHandling();
builder.AddPlatformCors();
builder.AddWebApiGeneratedConfiguration();
builder.AddGeneratedTransportLayers();
builder.RegisterGeneratedServices();
builder.AddPlatformTelemetry();
builder.AddPlatformAuthentication();
builder.AddPlatformHangfire();
builder.AddDatabase();
builder.AddPlatformHealthChecks();

var app = builder.Build();
app.UsePlatformExceptionHandling();
app.UsePlatformCors();
app.UsePlatformAuthentication();
app.MapGeneratedTransportLayers();
app.MapGeneratedGraphQlPlayground();
app.UsePlatformHangfire();
app.MapPlatformOpenApi();
app.UseDatabase();
app.MapPlatformHealthChecks();

app.Run();