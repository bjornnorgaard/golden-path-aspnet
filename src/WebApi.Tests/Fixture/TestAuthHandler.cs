using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApi.Database;
using WebApi.Database.Models;

namespace WebApi.Tests.Fixture;

/// <summary>
/// Stands in for a real authenticated session in tests: a request carrying <see cref="AuthenticatedHeader"/>
/// is treated as already authenticated as <see cref="AllowedLogin"/>, and any other request is treated
/// as anonymous - so integration tests exercise the same global fallback authorization policy as
/// production (see AuthenticationConfiguration) without needing a real login. <see cref="TestBase.Client"/>
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
    public const string AllowedEmail = "test-user@example.com";
    public const string AuthenticatedHeader = "X-Test-Authenticated";
    public static readonly UserId TestUserId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey(AuthenticatedHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var db = Context.RequestServices.GetRequiredService<TodoContext>();
        await db.Database.ExecuteSqlRawAsync(
            // Unqualified DO NOTHING (rather than ON CONFLICT ("Id")) so a race between two hosts'
            // very first authenticated request - each targeting this same fixed row - can't still
            // trip the separate unique index on Email once the Id-only conflict is resolved.
            @"INSERT INTO ""Users"" (""Id"", ""Email"", ""DisplayName"", ""GivenName"", ""FamilyName"", ""AvatarUrl"")
              VALUES ('11111111-1111-1111-1111-111111111111', 'test-user@example.com', 'test-user', 'Test', 'User', 'https://example.com/avatar.png')
              ON CONFLICT DO NOTHING;",
            Context.RequestAborted);

        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
            new Claim(ClaimTypes.Name, AllowedLogin),
            new Claim(ClaimTypes.Email, AllowedEmail)
        ], SchemeName);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
