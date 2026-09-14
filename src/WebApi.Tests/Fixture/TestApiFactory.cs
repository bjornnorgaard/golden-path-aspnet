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

        // The Google and Facebook schemes still get constructed on every request (ASP.NET Core's
        // authentication middleware probes every registered remote-auth scheme to see if the request
        // matches its callback path), so their options must pass OAuthOptions.Validate() even though
        // neither provider is ever exercised in tests.
        builder.UseSetting("Authentication:Google:ClientId", "test-client-id");
        builder.UseSetting("Authentication:Google:ClientSecret", "test-client-secret");
        builder.UseSetting("Authentication:Facebook:ClientId", "test-client-id");
        builder.UseSetting("Authentication:Facebook:ClientSecret", "test-client-secret");

        // Swap the real Google/Facebook/cookie login for a fake scheme that authenticates every
        // request as the test user, so tests exercise the real fallback authorization policy
        // without driving an actual OAuth flow.
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
