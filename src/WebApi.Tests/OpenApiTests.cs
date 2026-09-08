using System.Net;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

public sealed class OpenApiTests : TestBase
{
    [Test]
    public async Task GetTodosContract_ReturnsTheContractFileVerbatim()
    {
        var response = await Client.GetAsync("/openapi/todos.yaml");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/yaml");

        var served = await response.Content.ReadAsStringAsync();
        var onDisk = await File.ReadAllTextAsync(FindContractPath());

        await Assert.That(served).IsEqualTo(onDisk);
    }

    [Test]
    public async Task ApiReference_IsAvailable()
    {
        var response = await Client.GetAsync("/scalar/todos");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private static string FindContractPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WebApi.Tests.csproj")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "..", "WebApi", "Contracts", "OpenApi", "todos.openapi.yaml");
    }
}
