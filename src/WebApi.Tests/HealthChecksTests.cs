using System.Net;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

public sealed class HealthChecksTests : TestBase
{
    [Test]
    public async Task Health_ReturnsHealthy()
    {
        var response = await Client.GetAsync("/health");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await response.Content.ReadAsStringAsync()).IsEqualTo("Healthy");
    }

    [Test]
    public async Task Ready_DatabaseReachable_ReturnsHealthy()
    {
        var response = await Client.GetAsync("/ready");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await response.Content.ReadAsStringAsync()).IsEqualTo("Healthy");
    }
}
