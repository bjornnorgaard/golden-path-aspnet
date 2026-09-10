using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WebApi.Tests.Fixture;

/// <summary>
/// Stands in for a real GitHub session in tests: a request carrying <see cref="AuthenticatedHeader"/>
/// is treated as already authenticated as <see cref="AllowedLogin"/>, and any other request is treated
/// as anonymous - so integration tests exercise the same global fallback authorization policy as
/// production (see AuthenticationConfiguration) without needing a real GitHub login. <see cref="TestBase.Client"/>
/// adds the header by default, so only tests that build their own client (see RouteAuthorizationTests)
/// ever see the anonymous path.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string AllowedLogin = "test-user";
    public const string AuthenticatedHeader = "X-Test-Authenticated";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey(AuthenticatedHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, AllowedLogin)], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
