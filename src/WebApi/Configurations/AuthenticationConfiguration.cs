using System.Security.Claims;
using System.Text.Json;
using AspNet.Security.OAuth.Apple;
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
/// Configures the cookie session, external login providers, and application authorization: the
/// fallback policy below requires an authenticated session for every request - REST endpoints,
/// GraphQL, the Hangfire dashboard, and the Scalar/OpenAPI docs alike - except those explicitly
/// mapped with [AllowAnonymous].
/// </summary>
/// <remarks>
/// Google, Facebook, and Apple are each opted into separately by <see cref="AddPlatformAuthentication"/>
/// calling one line per provider - <see cref="AddGoogleAuthentication"/>,
/// <see cref="AddFacebookAuthentication"/>, <see cref="AddAppleAuthentication"/> - so the list of
/// providers the app actually supports lives in one place rather than spread across Program.cs.
/// A provider is "enabled" purely by whether that line is uncommented there; there's no separate
/// on/off setting. Calling one commits to that provider being fully configured: it reads its own
/// appsettings.json section and throws immediately (see <see cref="RequireConfigured"/>) if a
/// required value like ClientId/ClientSecret was left blank, rather than deferring to the OAuth
/// handler's own validation, which only runs lazily on the first incoming request (and, because
/// ASP.NET Core's authentication middleware constructs every registered remote-auth scheme's
/// options on every request to check whether it owns the callback path, would then fail every
/// request rather than just the login flow).
/// </remarks>
public static class AuthenticationConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformAuthentication()
        {
            builder.Services.AddHttpContextAccessor();

            builder.Services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(cookie =>
                {
                    cookie.LoginPath = "/login";
                    cookie.AccessDeniedPath = "/access-denied";
                    cookie.ExpireTimeSpan = TimeSpan.FromDays(14);
                });

            builder.Services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());

            // The current cluster only has Google credentials - uncomment the others here once
            // Authentication:Facebook/Apple are configured.
            builder.AddGoogleAuthentication();
            // builder.AddFacebookAuthentication();
            // builder.AddAppleAuthentication();
        }

        /// <summary>
        /// Requires <see cref="AddPlatformAuthentication"/> to have run first. Throws if
        /// Authentication:Google:ClientId/ClientSecret aren't both set.
        /// </summary>
        public void AddGoogleAuthentication()
        {
            var google = builder.Configuration.GetAuthentication().Google;
            RequireConfigured("Google",
                (nameof(google.ClientId), google.ClientId),
                (nameof(google.ClientSecret), google.ClientSecret));

            builder.Services.AddAuthentication().AddGoogle(options =>
            {
                options.ClientId = google.ClientId;
                options.ClientSecret = google.ClientSecret;
                options.CallbackPath = google.CallbackPath;

                options.Events.OnCreatingTicket = context => UpsertUserAsync(
                    context.HttpContext,
                    context.Identity,
                    email: context.Principal?.FindFirst(ClaimTypes.Email)?.Value ?? context.Principal?.FindFirst("email")?.Value,
                    displayName: context.Principal?.FindFirst(ClaimTypes.Name)?.Value ?? context.Principal?.FindFirst("name")?.Value,
                    givenName: context.Principal?.FindFirst(ClaimTypes.GivenName)?.Value ?? context.Principal?.FindFirst("given_name")?.Value,
                    familyName: context.Principal?.FindFirst(ClaimTypes.Surname)?.Value ?? context.Principal?.FindFirst("family_name")?.Value,
                    avatarUrl: context.Principal?.FindFirst("picture")?.Value ?? context.Principal?.FindFirst("urn:google:image_url")?.Value);

                options.Events.OnRemoteFailure = OnRemoteFailure;
            });
        }

        /// <summary>
        /// Requires <see cref="AddPlatformAuthentication"/> to have run first. Throws if
        /// Authentication:Facebook:ClientId/ClientSecret aren't both set.
        /// </summary>
        public void AddFacebookAuthentication()
        {
            var facebook = builder.Configuration.GetAuthentication().Facebook;
            RequireConfigured("Facebook",
                (nameof(facebook.ClientId), facebook.ClientId),
                (nameof(facebook.ClientSecret), facebook.ClientSecret));

            builder.Services.AddAuthentication().AddFacebook(options =>
            {
                options.AppId = facebook.ClientId;
                options.AppSecret = facebook.ClientSecret;
                options.CallbackPath = facebook.CallbackPath;

                options.Fields.Add("first_name");
                options.Fields.Add("last_name");
                options.Fields.Add("picture");

                options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "first_name");
                options.ClaimActions.MapJsonKey(ClaimTypes.Surname, "last_name");
                options.ClaimActions.MapCustomJson("urn:facebook:picture", user =>
                    user.TryGetProperty("picture", out var picture) &&
                    picture.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("url", out var url)
                        ? url.GetString()
                        : null);

                options.Events.OnCreatingTicket = context => UpsertUserAsync(
                    context.HttpContext,
                    context.Identity,
                    email: context.Principal?.FindFirst(ClaimTypes.Email)?.Value ?? context.Principal?.FindFirst("email")?.Value,
                    displayName: context.Principal?.FindFirst(ClaimTypes.Name)?.Value ?? context.Principal?.FindFirst("name")?.Value,
                    givenName: context.Principal?.FindFirst(ClaimTypes.GivenName)?.Value,
                    familyName: context.Principal?.FindFirst(ClaimTypes.Surname)?.Value,
                    avatarUrl: context.Principal?.FindFirst("urn:facebook:picture")?.Value);

                options.Events.OnRemoteFailure = OnRemoteFailure;
            });
        }

        /// <summary>
        /// Requires <see cref="AddPlatformAuthentication"/> to have run first. Throws if
        /// Authentication:Apple:ClientId/TeamId/KeyId/PrivateKey aren't all set.
        /// </summary>
        public void AddAppleAuthentication()
        {
            var apple = builder.Configuration.GetAuthentication().Apple;
            RequireConfigured("Apple",
                (nameof(apple.ClientId), apple.ClientId),
                (nameof(apple.TeamId), apple.TeamId),
                (nameof(apple.KeyId), apple.KeyId),
                (nameof(apple.PrivateKey), apple.PrivateKey));

            builder.Services.AddAuthentication().AddApple(options =>
            {
                options.ClientId = apple.ClientId;
                options.TeamId = apple.TeamId;
                options.KeyId = apple.KeyId;
                options.CallbackPath = apple.CallbackPath;

                // Apple never issues a static client secret (unlike Google/Facebook) - it must be a
                // freshly-signed JWT per token exchange, which the library builds for us from the
                // .p8 private key below.
                options.GenerateClientSecret = true;
                options.PrivateKey = (_, _) => Task.FromResult(apple.PrivateKey.AsMemory());

                options.Events.OnCreatingTicket = context =>
                {
                    var (givenName, familyName) = ParseAppleUserName(context.HttpContext);
                    var displayName = givenName is null && familyName is null
                        ? null
                        : string.Join(' ', new[] { givenName, familyName }.Where(name => !string.IsNullOrWhiteSpace(name)));

                    return UpsertUserAsync(
                        context.HttpContext,
                        context.Identity,
                        email: context.Principal?.FindFirst(ClaimTypes.Email)?.Value,
                        displayName: displayName,
                        givenName: givenName,
                        familyName: familyName,
                        avatarUrl: null);
                };

                options.Events.OnRemoteFailure = OnRemoteFailure;
            });
        }
    }

    /// <summary>
    /// Fails immediately, with a clear message, when a provider's extension method is called but one
    /// of its required settings was left at its blank appsettings.json default.
    /// </summary>
    private static void RequireConfigured(string providerName, params ReadOnlySpan<(string Name, string? Value)> fields)
    {
        var missing = new List<string>();
        foreach (var (name, value) in fields)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                missing.Add(name);
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Add{providerName}Authentication was called but Authentication:{providerName} is missing configuration: {string.Join(", ", missing)}.");
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

    /// <summary>
    /// Apple never puts the user's name on the id_token or in any claim - it's sent, once, as a "user"
    /// form field (JSON: <c>{"name":{"firstName":...,"lastName":...},"email":...}</c>) alongside the
    /// very first authorization for a given app, and never again on subsequent logins.
    /// </summary>
    private static (string? GivenName, string? FamilyName) ParseAppleUserName(HttpContext httpContext)
    {
        if (!httpContext.Request.HasFormContentType)
        {
            return (null, null);
        }

        string? userJson = httpContext.Request.Form["user"];
        if (string.IsNullOrWhiteSpace(userJson))
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(userJson);
            if (document.RootElement.TryGetProperty("name", out var name))
            {
                var givenName = name.TryGetProperty("firstName", out var first) ? first.GetString() : null;
                var familyName = name.TryGetProperty("lastName", out var last) ? last.GetString() : null;
                return (givenName, familyName);
            }
        }
        catch (JsonException)
        {
            // Malformed or unexpected "user" payload - nothing to enrich the profile with.
        }

        return (null, null);
    }

    extension(WebApplication app)
    {
        public void UsePlatformAuthentication()
        {
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapGet("/login", async Task<IResult> (IAuthenticationSchemeProvider schemes, string? provider, string? returnUrl) =>
                {
                    var scheme = provider?.ToLowerInvariant() switch
                    {
                        null or "" or "google" => GoogleDefaults.AuthenticationScheme,
                        "facebook" => FacebookDefaults.AuthenticationScheme,
                        "apple" => AppleAuthenticationDefaults.AuthenticationScheme,
                        _ => null
                    };

                    // A recognized provider name isn't necessarily an enabled one: only providers
                    // whose Add{Provider}Authentication was called in Program.cs are registered, so
                    // one that wasn't wired up won't have a scheme here.
                    if (scheme is null || await schemes.GetSchemeAsync(scheme) is null)
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
