using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

/// <summary>
/// Verifies /login's provider selection (see AuthenticationConfiguration.UsePlatformAuthentication):
/// which external scheme gets challenged for a given `provider` query value, and that an
/// unrecognized or not-wired-up provider is rejected before any challenge is issued. Program.cs
/// only calls AddGoogleAuthentication today (see AuthenticationConfigurationTests for
/// Facebook/Apple's own extension-method behavior), so Facebook and Apple behave the same as any
/// other name /login doesn't recognize.
/// </summary>
public sealed class LoginTests : TestBase
{
    // Factory.CreateClient() (TUnit's tracing wrapper) has no overload for client options, so go
    // through .Inner to disable auto-redirect and observe the 302 challenge directly instead of it
    // being followed to an external host (see RouteAuthorizationTests for the same reasoning).
    private HttpClient AnonymousClient => field ??= Factory.Inner.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Test]
    [Arguments("/login")]
    [Arguments("/login?provider=")]
    [Arguments("/login?provider=google")]
    [Arguments("/login?provider=GOOGLE")]
    public async Task Login_WithGoogleOrNoProvider_ChallengesGoogle(string path)
    {
        // Act
        var response = await AnonymousClient.GetAsync(path);

        // Assert: a challenge redirects straight to Google's own authorize endpoint.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location?.Host).Contains("google.com");
    }

    [Test]
    [Arguments("twitter")]
    [Arguments("microsoft")]
    [Arguments("facebook")]
    [Arguments("apple")]
    public async Task Login_WithUnavailableProvider_ReturnsBadRequestWithoutChallenging(string provider)
    {
        // Act
        var response = await AnonymousClient.GetAsync($"/login?provider={provider}");

        // Assert: rejected before any external redirect, whether the name is entirely unknown
        // (twitter/microsoft) or recognized but never wired up in Program.cs (facebook/apple) - an
        // unavailable provider must never fall back to challenging Google by surprise.
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Login_WithReturnUrl_StillChallenges()
    {
        // Act: `returnUrl` is carried through AuthenticationProperties rather than affecting which
        // provider gets challenged - a smoke check that the two query parameters don't interfere.
        var response = await AnonymousClient.GetAsync("/login?provider=google&returnUrl=/todos");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location?.Host).Contains("google.com");
    }
}
