using System.Reflection;
using Scalar.AspNetCore;

namespace WebApi.Configurations;

/// <summary>
/// Contract-first: OpenAPI documents under Contracts/OpenApi are embedded at build time and served
/// verbatim, so the document Scalar renders is exactly the specification that drives code generation
/// (see Generators/OpenApi), not a reflection-derived approximation of it.
/// </summary>
public static class OpenApiConfiguration
{
    private const string ContractSuffix = ".openapi.yaml";

    extension(WebApplication app)
    {
        public void MapPlatformOpenApi()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var contracts = assembly.GetManifestResourceNames()
                .Where(static name => name.EndsWith(ContractSuffix, StringComparison.Ordinal))
                .ToArray();

            foreach (var resourceName in contracts)
            {
                var documentName = resourceName[..^ContractSuffix.Length];
                app.MapGet($"/openapi/{documentName}.yaml", () =>
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName)!;
                    using var reader = new StreamReader(stream);
                    return Results.Text(reader.ReadToEnd(), "application/yaml");
                }).ExcludeFromDescription();
            }

            app.MapScalarApiReference(options =>
            {
                options.WithTitle("API Reference");
                options.WithOpenApiRoutePattern("/openapi/{documentName}.yaml");
                foreach (var resourceName in contracts)
                {
                    options.AddDocument(resourceName[..^ContractSuffix.Length]);
                }
            });
        }
    }
}
