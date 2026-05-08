using Microsoft.AspNetCore.Builder;
using Platform.Configurations;

namespace Platform;

public static class PlatformConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatform()
        {
            builder.AddPlatformTelemetry();
            builder.AddPlatformOpenApi();
        }
    }

    extension(WebApplication app)
    {
        public void UsePlatform()
        {
            app.UsePlatformTelemetry();
            app.MapPlatformOpenApi();
        }
    }
}