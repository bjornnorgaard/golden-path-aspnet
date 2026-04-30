using Microsoft.AspNetCore.Builder;

namespace Platform.Configurations;

public static class OpenApiConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformOpenApi()
        {
        }
    }

    extension(WebApplication app)
    {
        public void MapPlatformOpenApi()
        {
        }
    }
}