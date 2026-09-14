using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using WebApi.Database;
using WebApi.Database.Models;

namespace WebApi.Configurations;

/// <summary>
/// Configures Google and Facebook authentication (Apple to follow) and application authorization:
/// the fallback policy below requires an authenticated session for every request - REST endpoints,
/// GraphQL, the Hangfire dashboard, and the Scalar/OpenAPI docs alike - except those explicitly
/// mapped with [AllowAnonymous].
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

                    google.Events.OnCreatingTicket = context => UpsertUserAsync(
                        context.HttpContext,
                        context.Identity,
                        email: context.Principal?.FindFirst(ClaimTypes.Email)?.Value ?? context.Principal?.FindFirst("email")?.Value,
                        displayName: context.Principal?.FindFirst(ClaimTypes.Name)?.Value ?? context.Principal?.FindFirst("name")?.Value,
                        givenName: context.Principal?.FindFirst(ClaimTypes.GivenName)?.Value ?? context.Principal?.FindFirst("given_name")?.Value,
                        familyName: context.Principal?.FindFirst(ClaimTypes.Surname)?.Value ?? context.Principal?.FindFirst("family_name")?.Value,
                        avatarUrl: context.Principal?.FindFirst("picture")?.Value ?? context.Principal?.FindFirst("urn:google:image_url")?.Value);

                    google.Events.OnRemoteFailure = OnRemoteFailure;
                })
                .AddFacebook(facebook =>
                {
                    facebook.AppId = options.Facebook.ClientId;
                    facebook.AppSecret = options.Facebook.ClientSecret;
                    facebook.CallbackPath = options.Facebook.CallbackPath;

                    facebook.Fields.Add("first_name");
                    facebook.Fields.Add("last_name");
                    facebook.Fields.Add("picture");

                    facebook.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "first_name");
                    facebook.ClaimActions.MapJsonKey(ClaimTypes.Surname, "last_name");
                    facebook.ClaimActions.MapCustomJson("urn:facebook:picture", user =>
                        user.TryGetProperty("picture", out var picture) &&
                        picture.TryGetProperty("data", out var data) &&
                        data.TryGetProperty("url", out var url)
                            ? url.GetString()
                            : null);

                    facebook.Events.OnCreatingTicket = context => UpsertUserAsync(
                        context.HttpContext,
                        context.Identity,
                        email: context.Principal?.FindFirst(ClaimTypes.Email)?.Value ?? context.Principal?.FindFirst("email")?.Value,
                        displayName: context.Principal?.FindFirst(ClaimTypes.Name)?.Value ?? context.Principal?.FindFirst("name")?.Value,
                        givenName: context.Principal?.FindFirst(ClaimTypes.GivenName)?.Value,
                        familyName: context.Principal?.FindFirst(ClaimTypes.Surname)?.Value,
                        avatarUrl: context.Principal?.FindFirst("urn:facebook:picture")?.Value);

                    facebook.Events.OnRemoteFailure = OnRemoteFailure;
                });

            builder.Services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());
        }
    }

    /// <summary>
    /// Shared by every external login provider: upserts the local <see cref="User"/> by email and
    /// attaches its id as the <see cref="ClaimTypes.NameIdentifier"/> claim on the resulting identity.
    /// </summary>
    private static async Task UpsertUserAsync(
        HttpContext httpContext,
        ClaimsIdentity? identity,
        string? email,
        string? displayName,
        string? givenName,
        string? familyName,
        string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var db = httpContext.RequestServices.GetRequiredService<TodoContext>();
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

        identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
    }

    private static Task OnRemoteFailure(RemoteFailureContext context)
    {
        context.HandleResponse();
        context.Response.Redirect("/access-denied");
        return Task.CompletedTask;
    }

    extension(WebApplication app)
    {
        public void UsePlatformAuthentication()
        {
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapGet("/login", IResult (string? provider, string? returnUrl) =>
                {
                    var scheme = provider?.ToLowerInvariant() switch
                    {
                        null or "" or "google" => GoogleDefaults.AuthenticationScheme,
                        "facebook" => FacebookDefaults.AuthenticationScheme,
                        _ => null
                    };

                    if (scheme is null)
                    {
                        return Results.BadRequest($"Unsupported authentication provider '{provider}'.");
                    }

                    return Results.Challenge(
                        new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
                        [scheme]);
                })
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