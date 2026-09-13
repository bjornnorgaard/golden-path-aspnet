using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;

namespace WebApi.Configurations;

/// <summary>
/// Configures Google authentication and application authorization: the fallback policy below
/// requires an authenticated session for every request - REST endpoints, GraphQL, the Hangfire dashboard,
/// and the Scalar/OpenAPI docs alike - except those explicitly mapped with [AllowAnonymous].
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
                .AddGoogle(google =>
                {
                    google.ClientId = options.Google.ClientId;
                    google.ClientSecret = options.Google.ClientSecret;
                    google.CallbackPath = options.Google.CallbackPath;

                    google.Events.OnRemoteFailure = context =>
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
                    [GoogleDefaults.AuthenticationScheme]))
                .AllowAnonymous();

            app.MapPost("/logout", async (HttpContext http) =>
                {
                    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return Results.Redirect("/");
                })
                .AllowAnonymous();

            app.MapGet("/access-denied", () => Results.Text(
                    "Your account is not authorized to use this service.",
                    "text/plain",
                    statusCode: StatusCodes.Status403Forbidden))
                .AllowAnonymous();
        }
    }
}
