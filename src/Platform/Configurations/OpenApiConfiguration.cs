using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Configurations;

public static class OpenApiConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformOpenApi()
        {
            builder.Services.AddOpenApi();
        }
    }

    extension(WebApplication app)
    {
        public void MapPlatformOpenApi()
        {
            app.MapOpenApi();
        }
    }
}