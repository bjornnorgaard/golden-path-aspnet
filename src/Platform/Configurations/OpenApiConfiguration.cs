using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;

namespace Platform.Configurations;

public static class OpenApiConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformOpenApi()
        {
            builder.Services.AddOpenApi(options =>
            {
                options.CreateSchemaReferenceId = static typeInfo =>
                {
                    var name = typeInfo.Type.Name;

                    if (typeInfo.Type.FullName?.Contains('+') ?? false)
                    {
                        name = typeInfo.Type.FullName.Split(".").Last();
                        name = name.Replace("+", "");
                    }

                    return name;
                };
            });
        }
    }

    extension(WebApplication app)
    {
        public void MapPlatformOpenApi()
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options => options.WithTitle("API Reference"));
        }
    }
}