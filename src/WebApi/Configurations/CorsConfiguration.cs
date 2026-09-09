namespace WebApi.Configurations;

public static class CorsConfiguration
{
    private const string PolicyName = "GoldenPathCors";

    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformCors()
        {
            var allowedOrigins = builder.Configuration.GetCors().AllowedOrigins;

            builder.Services.AddCors(options => options.AddPolicy(PolicyName, policy =>
            {
                // No origins configured means no cross-origin requests are allowed. Set
                // Cors:AllowedOrigins in appsettings to enable a browser-based client.
                if (allowedOrigins.Count > 0)
                {
                    policy.WithOrigins(allowedOrigins.ToArray())
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
            }));
        }
    }

    extension(WebApplication app)
    {
        public void UsePlatformCors() => app.UseCors(PolicyName);
    }
}
