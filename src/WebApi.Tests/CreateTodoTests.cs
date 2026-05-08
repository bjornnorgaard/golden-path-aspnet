using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Database;
using WebApi.Endpoints;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

public class CreateTodoTests : TestBase
{
    [Test]
    public async Task CreateTodo_Success()
    {
        var response = await Client.PostAsJsonAsync(Routes.Todos.Create, new { Title = "Test Todo" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(response.Headers.Location).IsNotNull();

        var get = await Client.GetAsync(response.Headers.Location!);
        await Assert.That(get.StatusCode).IsEqualTo(HttpStatusCode.OK);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
        var exists = await db.Todos.AsNoTracking().AnyAsync(t => t.Title == "Test Todo");
        await Assert.That(exists).IsTrue();
    }
}