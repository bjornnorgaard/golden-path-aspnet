using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using WebApi.Configurations;

namespace WebApi.Tests;

/// <summary>
/// Verifies the contract each Add{Provider}Authentication extension method promises (see
/// AuthenticationConfiguration): calling one commits to that provider being fully configured, so it
/// throws immediately when a required setting was left blank, and registers cleanly otherwise. This
/// is checked directly against a bare builder rather than through Program.cs/TestApiFactory, since
/// Program.cs currently only calls AddGoogleAuthentication - Facebook and Apple have no other
/// integration-test coverage.
/// </summary>
public sealed class AuthenticationConfigurationTests
{
    // GetAuthentication() binds the whole "Authentication" section at once, so every provider's
    // required fields must resolve to *some* value even when the test only cares about one of them
    // - these are valid, unrelated to whichever fields a given test overrides below.
    private static readonly Dictionary<string, string?> DefaultSettings = new()
    {
        ["Authentication:Google:ClientId"] = "google-id",
        ["Authentication:Google:ClientSecret"] = "google-secret",
        ["Authentication:Google:CallbackPath"] = "/auth/callback/google",
        ["Authentication:Facebook:ClientId"] = "facebook-id",
        ["Authentication:Facebook:ClientSecret"] = "facebook-secret",
        ["Authentication:Facebook:CallbackPath"] = "/auth/callback/facebook",
        ["Authentication:Apple:ClientId"] = "apple-id",
        ["Authentication:Apple:TeamId"] = "apple-team",
        ["Authentication:Apple:KeyId"] = "apple-key",
        ["Authentication:Apple:PrivateKey"] = "apple-private-key",
        ["Authentication:Apple:CallbackPath"] = "/auth/callback/apple"
    };

    private static WebApplicationBuilder CreateBuilder(params (string Key, string Value)[] overrides)
    {
        var settings = new Dictionary<string, string?>(DefaultSettings);
        foreach (var (key, value) in overrides)
        {
            settings[key] = value;
        }

        // Deliberately doesn't call AddPlatformAuthentication: that now calls AddGoogleAuthentication
        // itself (see AuthenticationConfiguration), and these tests need to invoke each provider's
        // method themselves to observe its own throw/succeed behavior in isolation.
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(settings);
        return builder;
    }

    [Test]
    public async Task AddGoogleAuthentication_WithFullConfiguration_ThrowsNothing()
    {
        var builder = CreateBuilder();

        await Assert.That(() => builder.AddGoogleAuthentication()).ThrowsNothing();
    }

    [Test]
    [Arguments("", "secret")]
    [Arguments("id", "")]
    public async Task AddGoogleAuthentication_WithMissingConfiguration_Throws(string clientId, string clientSecret)
    {
        var builder = CreateBuilder(
            ("Authentication:Google:ClientId", clientId),
            ("Authentication:Google:ClientSecret", clientSecret));

        await Assert.That(() => builder.AddGoogleAuthentication()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task AddFacebookAuthentication_WithFullConfiguration_ThrowsNothing()
    {
        var builder = CreateBuilder();

        await Assert.That(() => builder.AddFacebookAuthentication()).ThrowsNothing();
    }

    [Test]
    [Arguments("", "secret")]
    [Arguments("id", "")]
    public async Task AddFacebookAuthentication_WithMissingConfiguration_Throws(string clientId, string clientSecret)
    {
        var builder = CreateBuilder(
            ("Authentication:Facebook:ClientId", clientId),
            ("Authentication:Facebook:ClientSecret", clientSecret));

        await Assert.That(() => builder.AddFacebookAuthentication()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task AddAppleAuthentication_WithFullConfiguration_ThrowsNothing()
    {
        var builder = CreateBuilder();

        await Assert.That(() => builder.AddAppleAuthentication()).ThrowsNothing();
    }

    [Test]
    [Arguments("", "team", "key", "private-key")]
    [Arguments("id", "", "key", "private-key")]
    [Arguments("id", "team", "", "private-key")]
    [Arguments("id", "team", "key", "")]
    public async Task AddAppleAuthentication_WithMissingConfiguration_Throws(
        string clientId, string teamId, string keyId, string privateKey)
    {
        var builder = CreateBuilder(
            ("Authentication:Apple:ClientId", clientId),
            ("Authentication:Apple:TeamId", teamId),
            ("Authentication:Apple:KeyId", keyId),
            ("Authentication:Apple:PrivateKey", privateKey));

        await Assert.That(() => builder.AddAppleAuthentication()).Throws<InvalidOperationException>();
    }
}
