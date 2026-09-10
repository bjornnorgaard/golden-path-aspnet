using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace WebApi.Configurations;

/// <summary>
/// Locks the whole service behind GitHub login: nothing carries [AllowAnonymous] except /login,
/// /logout and /access-denied, so the fallback policy below requires an authenticated session for
/// every other request - REST endpoints, GraphQL, the Hangfire dashboard, and the Scalar/OpenAPI docs
/// alike. Authentication only proves who someone is, so GitHub's OAuth ticket is additionally rejected
/// in <c>OnCreatingTicket</c> for anyone other than the configured <c>Authentication:AllowedGitHubLogin</c>.
/// </summary>
public static class AuthenticationConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformAuthentication()
        {
            var options = builder.Configuration.GetAuthentication();

            builder.Services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(cookie =>
                {
                    cookie.LoginPath = "/login";
                    cookie.AccessDeniedPath = "/access-denied";
                    cookie.ExpireTimeSpan = TimeSpan.FromDays(14);
                })
                .AddGitHub(github =>
                {
                    github.ClientId = options.GitHub.ClientId;
                    github.ClientSecret = options.GitHub.ClientSecret;
                    github.CallbackPath = "/auth/callback/github";
                    github.Scope.Add("read:user");

                    github.Events.OnCreatingTicket = context =>
                    {
                        var login = context.Identity?.FindFirst(ClaimTypes.Name)?.Value;
                        if (!string.Equals(login, options.AllowedGitHubLogin, StringComparison.OrdinalIgnoreCase))
                        {
                            context.Fail($"GitHub user '{login}' is not authorized for this application.");
                        }

                        return Task.CompletedTask;
                    };

                    github.Events.OnRemoteFailure = context =>
                    {
                        context.HandleResponse();
                        context.Response.Redirect("/access-denied");
                        return Task.CompletedTask;
                    };
                });

            builder.Services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());
        }
    }

    extension(WebApplication app)
    {
        public void UsePlatformAuthentication()
        {
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapGet("/login", (string? returnUrl) => Results.Challenge(
                    new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
                    [GitHubAuthenticationDefaults.AuthenticationScheme]))
                .AllowAnonymous();

            app.MapPost("/logout", async (HttpContext http) =>
                {
                    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return Results.Redirect("/");
                })
                .AllowAnonymous();

            app.MapGet("/access-denied", () => Results.Text(
                    "Your GitHub account is not authorized to use this service.",
                    "text/plain",
                    statusCode: StatusCodes.Status403Forbidden))
                .AllowAnonymous();
        }
    }
}
