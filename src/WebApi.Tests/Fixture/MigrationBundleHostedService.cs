using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace WebApi.Tests.Fixture;

public sealed record MigrationBundleOptions(string BundlePath, string ConnectionString);

/// <summary>
/// Applies EF Core migrations using the pre-built migrations bundle.
/// Runs during app startup so tests don't have to invoke it explicitly.
/// </summary>
public sealed class MigrationBundleHostedService(MigrationBundleOptions options) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = options.BundlePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Arguments = $"--connection \"{options.ConnectionString}\""
        };

        var bundleProcess = Process.Start(psi);
        if (bundleProcess == null)
        {
            throw new InvalidOperationException("Failed to start efbundle process.");
        }

        var stderr = await bundleProcess.StandardError.ReadToEndAsync(ct);
        await bundleProcess.WaitForExitAsync(ct);

        if (bundleProcess.ExitCode != 0)
        {
            throw new Exception($"efbundle failed: {stderr}");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}