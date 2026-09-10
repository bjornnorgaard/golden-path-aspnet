using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using WebApi.Tests.Contracts;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

/// <summary>
/// Pins down exactly which routes the global fallback authorization policy (see
/// AuthenticationConfiguration) actually covers, so that if a route's intended visibility ever
/// changes - e.g. making the Scalar docs public - the change shows up here as a deliberate edit
/// instead of silently in production.
/// <para>
/// Requests are sent through <see cref="AnonymousClient"/>, which never carries
/// <see cref="TestAuthHandler.AuthenticatedHeader"/>, so <c>TestAuthHandler</c> reports them as
/// unauthenticated exactly like a real anonymous caller. Auto-redirect is disabled so a 302 (e.g.
/// the cookie challenge, or GitHub's own authorize redirect) is observable directly instead of
/// being followed to an external host.
/// </para>
/// </summary>
public sealed class RouteAuthorizationTests : TestBase
{
    // Factory.CreateClient() (TUnit's tracing wrapper) has no overload for client options, so go
    // through .Inner - the plain Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory - to disable
    // auto-redirect and observe a 302 directly instead of it being followed to an external host.
    private HttpClient AnonymousClient => field ??= Factory.Inner.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Test]
    [Arguments("POST", TestRoutes.Todos.Create)]
    [Arguments("POST", TestRoutes.Todos.GetList)]
    [Arguments("POST", "/graphql")]
    [Arguments("GET", "/graphql/playground")]
    [Arguments("GET", "/scalar/todos")]
    [Arguments("GET", "/openapi/todos.yaml")]
    [Arguments("GET", "/hangfire")]
    public async Task ProtectedRoute_WithoutAuthentication_IsRejected(string method, string path)
    {
        // Act: anonymous request against a route expected to require login.
        var response = await AnonymousClient.SendAsync(BuildRequest(method, path));

        // Assert: the fallback policy challenges it before the endpoint ever runs - never a 200,
        // and never a 404 (which would mean the route wasn't actually being exercised at all).
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.OK);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    [Arguments("POST", TestRoutes.Todos.Create)]
    [Arguments("POST", TestRoutes.Todos.GetList)]
    [Arguments("POST", "/graphql")]
    [Arguments("GET", "/graphql/playground")]
    [Arguments("GET", "/scalar/todos")]
    [Arguments("GET", "/openapi/todos.yaml")]
    [Arguments("GET", "/hangfire")]
    public async Task ProtectedRoute_WithAuthentication_IsReachable(string method, string path)
    {
        // Arrange: the same route and method as the negative case above, but now authenticated -
        // proves a 401/302 there is actually caused by the missing session, not an unrelated
        // routing problem that would fail regardless of authentication.
        var request = BuildRequest(method, path);
        request.Headers.Add(TestAuthHandler.AuthenticatedHeader, "true");

        // Act
        var response = await AnonymousClient.SendAsync(request);

        // Assert
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    [Arguments("GET", "/login")]
    [Arguments("POST", "/logout")]
    [Arguments("GET", "/access-denied")]
    public async Task PublicRoute_WithoutAuthentication_IsNotBlockedByAuthorization(string method, string path)
    {
        // Act: these three routes exist specifically so a caller can reach them before having a
        // session, so they must stay reachable with no authentication at all.
        var response = await AnonymousClient.SendAsync(BuildRequest(method, path));

        // Assert: never 401 - each route's own logic decides its status (a redirect, or a 403 body
        // for /access-denied), but the authorization middleware itself must never intervene.
        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.Unauthorized);
    }

    private static HttpRequestMessage BuildRequest(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { });
        }

        return request;
    }
}
