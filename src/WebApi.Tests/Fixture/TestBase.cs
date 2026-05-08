using TUnit.AspNetCore;

namespace WebApi.Tests.Fixture;

public abstract class TestBase : WebApplicationTest<TestApiFactory, Program>
{
    protected HttpClient Client => field ??= Factory.CreateClient();
}