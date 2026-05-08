using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TUnit.AspNetCore;

namespace WebApi.Tests.Fixture;

public sealed class TestApiFactory : TestWebApplicationFactory<Program>
{
    [ClassDataSource<PostgresContainer>(Shared = SharedType.PerTestSession)]
    public PostgresContainer Postgres { get; init; } = null!;

    [ClassDataSource<EfBundle>(Shared = SharedType.PerTestSession)]
    public EfBundle Bundle { get; init; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var cs = Postgres.GetConnectionString();

        builder.UseSetting("ConnectionStrings:DefaultConnection", cs);

        builder.ConfigureServices(services =>
        {
            // Add migration bundle as a hosted service so it runs during app startup.
            services.AddSingleton(new MigrationBundleOptions(Bundle.BundlePath, cs));
            services.AddHostedService<MigrationBundleHostedService>();
        });
    }
}