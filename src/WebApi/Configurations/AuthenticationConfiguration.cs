using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using WebApi.Database;
using WebApi.Database.Models;

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

            builder.Services.AddHttpContextAccessor();

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

                    google.Events.OnCreatingTicket = async context =>
                    {
                        var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value
                                    ?? context.Principal?.FindFirst("email")?.Value;
                        var displayName = context.Principal?.FindFirst(ClaimTypes.Name)?.Value
                                          ?? context.Principal?.FindFirst("name")?.Value;
                        var givenName = context.Principal?.FindFirst(ClaimTypes.GivenName)?.Value
                                        ?? context.Principal?.FindFirst("given_name")?.Value;
                        var familyName = context.Principal?.FindFirst(ClaimTypes.Surname)?.Value
                                         ?? context.Principal?.FindFirst("family_name")?.Value;
                        var avatarUrl = context.Principal?.FindFirst("picture")?.Value
                                        ?? context.Principal?.FindFirst("urn:google:image_url")?.Value;

                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            var db = context.HttpContext.RequestServices.GetRequiredService<TodoContext>();
                            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
                            if (user is null)
                            {
                                user = new User
                                {
                                    Id = UserId.New(),
                                    Email = email,
                                    DisplayName = displayName,
                                    GivenName = givenName,
                                    FamilyName = familyName,
                                    AvatarUrl = avatarUrl
                                };
                                await db.Users.AddAsync(user);
                                await db.SaveChangesAsync();
                            }
                            else
                            {
                                user.DisplayName = displayName ?? user.DisplayName;
                                user.GivenName = givenName ?? user.GivenName;
                                user.FamilyName = familyName ?? user.FamilyName;
                                user.AvatarUrl = avatarUrl ?? user.AvatarUrl;
                                await db.SaveChangesAsync();
                            }

                            if (context.Identity is not null)
                            {
                                context.Identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
                            }
                        }
                    };

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