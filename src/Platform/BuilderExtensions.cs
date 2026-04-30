using Microsoft.AspNetCore.Builder;
using Platform.Configurations;

namespace Platform;

public static class BuilderExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatform()
        {
            builder.AddPlatformOpenApi();
        }
    }

    extension(WebApplication app)
    {
        public void UsePlatform()
        {
            app.MapPlatformOpenApi();
        }
    }
}