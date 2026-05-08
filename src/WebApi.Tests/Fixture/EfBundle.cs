using System.Diagnostics;
using TUnit.Core.Interfaces;

namespace WebApi.Tests.Fixture;

public sealed class EfBundle : IAsyncInitializer
{
    public string BundlePath { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var srcDir = Path.Combine(repoRoot, "src");

        BundlePath = Path.Combine(Path.GetTempPath(), "goldenpath-webapi-efbundle");

        if (File.Exists(BundlePath))
        {
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = srcDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Arguments =
                "tool run dotnet-ef -- migrations bundle " +
                "--target-runtime osx-arm64 " +
                "--configuration Release " +
                "--project ./WebApi/WebApi.csproj " +
                $"--output \"{BundlePath}\""
        };

        var p = Process.Start(psi)!;
        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();

        if (p.ExitCode != 0)
        {
            throw new Exception($"Failed to build efbundle.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
        }
    }

    private static string FindRepoRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "WebApi", "WebApi.csproj");
            if (File.Exists(candidate))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repo root containing src/WebApi/WebApi.csproj");
    }
}