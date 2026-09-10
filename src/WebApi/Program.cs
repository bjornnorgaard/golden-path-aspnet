using WebApi.Configurations;
using WebApi.Todos;
using WebApi.Todos.GraphQl;

var builder = WebApplication.CreateSlimBuilder(args);
builder.AddPlatformForwardedHeaders();
builder.AddPlatformExceptionHandling();
builder.AddPlatformCors();
builder.AddWebApiGeneratedConfiguration();
builder.AddGeneratedTransportLayers();
builder.AddPlatformAuthentication();
builder.RegisterGeneratedServices();
builder.AddPlatformHealthChecks();
builder.AddPlatformTelemetry();
builder.AddPlatformHangfire();
builder.AddDatabase();

var app = builder.Build();
app.UsePlatformForwardedHeaders();
app.UsePlatformExceptionHandling();
app.UsePlatformCors();
app.UsePlatformAuthentication();
app.MapGeneratedGraphQlPlayground();
app.MapGeneratedTransportLayers();
app.UsePlatformHealthChecks();
app.UsePlatformHangfire();
app.MapPlatformOpenApi();
app.UseDatabase();

app.Run();
