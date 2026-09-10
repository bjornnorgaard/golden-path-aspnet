using TUnit.AspNetCore;

namespace WebApi.Tests.Fixture;

public abstract class TestBase : WebApplicationTest<TestApiFactory, Program>
{
    /// <summary>
    /// Carries <see cref="TestAuthHandler.AuthenticatedHeader"/> on every request, so every test suite
    /// other than RouteAuthorizationTests keeps exercising its endpoints as an authenticated caller.
    /// </summary>
    protected HttpClient Client => field ??= CreateAuthenticatedClient();

    private HttpClient CreateAuthenticatedClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.AuthenticatedHeader, "true");
        return client;
    }
}