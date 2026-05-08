using TUnit.AspNetCore;

namespace WebApi.Tests;

public abstract class TestBase : WebApplicationTest<FixtureFactory, Program>
{
    protected HttpClient Client => field ??= Factory.CreateClient();
}