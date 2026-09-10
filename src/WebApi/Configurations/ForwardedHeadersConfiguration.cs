using Microsoft.AspNetCore.HttpOverrides;

namespace WebApi.Configurations;

/// <summary>
/// Mimir terminates TLS at its Gateway (Envoy) and forwards plain HTTP to this
/// pod, setting X-Forwarded-Proto/X-Forwarded-For. Without trusting those
/// headers, the app thinks every request is HTTP - wrong scheme in generated
/// redirect URLs (e.g. the /login challenge) and wrong request.IsHttps for any
/// secure-cookie/HTTPS-only logic. KnownNetworks/KnownProxies are cleared
/// because the proxy is the in-cluster Gateway on a dynamic pod IP, not a
/// fixed address this app could pin.
/// </summary>
public static class ForwardedHeadersConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformForwardedHeaders()
        {
            builder.Services.Configure<ForwardedHeadersOptions>(opts =>
            {
                opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                opts.KnownIPNetworks.Clear();
                opts.KnownProxies.Clear();
            });
        }
    }

    extension(WebApplication app)
    {
        public void UsePlatformForwardedHeaders()
        {
            app.UseForwardedHeaders();
        }
    }
}
