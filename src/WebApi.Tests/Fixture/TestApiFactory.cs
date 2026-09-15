using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
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

        // Program.cs only calls AddGoogleAuthentication (Facebook/Apple aren't wired up there yet -
        // see AuthenticationConfigurationTests for coverage of those extension methods in isolation),
        // so Google is the only provider whose configuration needs to pass validation here.
        builder.UseSetting("Authentication:Google:ClientId", "test-client-id");
        builder.UseSetting("Authentication:Google:ClientSecret", "test-client-secret");

        // Swap the real Google/cookie login for a fake scheme that authenticates every request as
        // the test user, so tests exercise the real fallback authorization policy without driving
        // an actual OAuth flow.
        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(auth =>
            {
                auth.DefaultScheme = TestAuthHandler.SchemeName;
                auth.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                auth.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });
        });
    }
}
