using Microsoft.AspNetCore.Builder;
using WebApi.Platform.Configurations;

namespace WebApi.Platform;

public static class PlatformConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatform()
        {
            builder.AddPlatformExceptionHandling();
            builder.AddPlatformTelemetry();
            builder.AddPlatformOpenApi();
        }
    }

    extension(WebApplication app)
    {
        public void UsePlatform()
        {
            app.UsePlatformExceptionHandling();
            app.MapPlatformOpenApi();
        }
    }
}
