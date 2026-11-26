using System.Diagnostics;
using System.Net;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using WebApi.Features.Todos;
using WebApi.Features.Users;

namespace WebApi.Configurations;

/// <summary>
/// gRPC as a third transport next to REST and GraphQL: the services under Features/* are thin
/// adapters over the same handlers. Contracts live in Contracts/Grpc/**/*.proto and are the source of
/// truth (Grpc.Tools generates the abstract service bases at build time).
/// <para>
/// Every gRPC endpoint is subject to the global authorization fallback policy (see
/// AuthenticationConfiguration) - streaming ones such as reflection included - except the standard
/// health service, which is mapped with AllowAnonymous like /healthz and /readyz. Reflection is
/// opt-in via Grpc:ReflectionEnabled and still requires authentication.
/// </para>
/// </summary>
public static class GrpcConfiguration
{
    /// <summary>The complete set of gRPC methods reachable without a session. Pinned by a test.</summary>
    public static readonly IReadOnlyList<string> AnonymousMethods =
    [
        "/grpc.health.v1.Health/Check",
        "/grpc.health.v1.Health/Watch"
    ];

    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformGrpc()
        {
            builder.Services.AddGrpc(options => options.Interceptors.Add<UnhandledExceptionInterceptor>());
            builder.Services.AddGrpcHealthChecks();

            var grpc = builder.Configuration.GetGrpc();
            if (grpc.ReflectionEnabled)
            {
                builder.Services.AddGrpcReflection();
            }

            if (grpc.Port > 0)
            {
                // gRPC needs HTTP/2, which Kestrel only offers on cleartext endpoints that are
                // HTTP/2-only (no TLS means no protocol negotiation). So gRPC gets its own port; terminate
                // TLS in front of it, as for HTTP.
                builder.WebHost.ConfigureKestrel((context, kestrel) =>
                {
                    kestrel.ListenAnyIP(grpc.Port, listen => listen.Protocols = HttpProtocols.Http2);

                    // Configuring an endpoint in code makes Kestrel drop the addresses from URLS /
                    // HTTP_PORTS, so bind those explicitly to keep serving REST and GraphQL as before.
                    foreach (var address in ConfiguredHttpAddresses(context.Configuration))
                    {
                        if (address.Host is "localhost")
                        {
                            kestrel.ListenLocalhost(address.Port);
                        }
                        else if (IPAddress.TryParse(address.Host.Trim('[', ']'), out var ip) && !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.IPv6Any))
                        {
                            kestrel.Listen(ip, address.Port);
                        }
                        else
                        {
                            kestrel.ListenAnyIP(address.Port);
                        }
                    }
                });
            }
        }
    }

    /// <summary>The HTTP addresses the host is configured with, as Kestrel would have bound them.</summary>
    private static IEnumerable<Uri> ConfiguredHttpAddresses(IConfiguration configuration)
    {
        var addresses = new List<Uri>();

        var urls = configuration["URLS"] ?? configuration["urls"];
        if (!string.IsNullOrWhiteSpace(urls))
        {
            foreach (var url in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                // Kestrel's wildcard hosts (*, +) are not valid in a Uri.
                if (Uri.TryCreate(url.Replace("://*", "://0.0.0.0").Replace("://+", "://0.0.0.0"), UriKind.Absolute, out var uri))
                {
                    addresses.Add(uri);
                }
            }
        }
        else if (configuration["HTTP_PORTS"] is { Length: > 0 } ports)
        {
            foreach (var port in ports.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(port, out var number))
                {
                    addresses.Add(new Uri($"http://0.0.0.0:{number}"));
                }
            }
        }

        // Neither is set: the framework default.
        return addresses.Count > 0 ? addresses : [new Uri("http://localhost:5000")];
    }

    extension(WebApplication app)
    {
        public void MapPlatformGrpc()
        {
            app.MapGrpcService<TodosGrpcService>();
            app.MapGrpcService<UsersGrpcService>();
            app.MapGrpcHealthChecksService().AllowAnonymous();

            if (app.Configuration.GetGrpc().ReflectionEnabled)
            {
                app.MapGrpcReflectionService();
            }
        }
    }

    /// <summary>
    /// Masks unexpected failures like <c>GlobalExceptionHandler</c> does for HTTP: the detail goes to
    /// the log and the trace, the caller only gets a generic INTERNAL. Expected outcomes are thrown as
    /// <see cref="RpcException"/> by the services (see <see cref="GrpcErrors"/>) and pass through.
    /// </summary>
    public sealed class UnhandledExceptionInterceptor(ILogger<UnhandledExceptionInterceptor> logger) : Interceptor
    {
        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await continuation(request, context);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                throw new RpcException(new Status(StatusCode.Cancelled, "The call was cancelled."));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while handling gRPC {Method}", context.Method);

                var activity = Activity.Current;
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, "Unhandled exception");
                activity?.SetTag("error.type", ex.GetType().FullName);

                throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
            }
        }
    }
}
