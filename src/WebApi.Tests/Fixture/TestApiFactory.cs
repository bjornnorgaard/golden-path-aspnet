using Microsoft.AspNetCore.Hosting;
using TUnit.AspNetCore;

namespace WebApi.Tests.Fixture;

public sealed class TestApiFactory : TestWebApplicationFactory<Program>
{
    [ClassDataSource<PostgresContainer>(Shared = SharedType.PerTestSession)]
    public PostgresContainer Postgres { get; init; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var cs = Postgres.GetConnectionString();

        builder.UseSetting("ConnectionStrings:DefaultConnection", cs);

        // Each factory gets its own Hangfire schema so hosts running in parallel don't have their
        // workers steal and abandon each other's jobs in the shared Postgres container.
        builder.UseSetting("Hangfire:SchemaName", $"hangfire_{Guid.NewGuid():N}");

        builder.UseSetting("Cors:AllowedOrigins:0", "https://allowed.test");
    }
}
