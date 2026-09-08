using WebApi.Platform.Configurations;
using WebApi.Todos;
using WebApi.Todos.GraphQl;

namespace WebApi.Platform;

public static class PlatformConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatform()
        {
            builder.AddPlatformExceptionHandling();
            builder.AddPlatformTelemetry();
            builder.AddWebApiGeneratedConfiguration();
            builder.RegisterGeneratedServices();
            builder.AddGeneratedTransportLayers();
        }
    }

    extension(WebApplication app)
    {
        public void UsePlatform()
        {
            app.UsePlatformExceptionHandling();
            app.MapPlatformOpenApi();
            app.MapGeneratedTransportLayers();
            app.MapGeneratedGraphQlPlayground();
        }
    }
}