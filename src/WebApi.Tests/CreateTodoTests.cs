using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Database;
using WebApi.Tests.Contracts;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

public class CreateTodoTests : TestBase
{
    public class Request
    {
        public required string Title { get; init; }
        public DateTime? DueBy { get; init; }
    }

    public class Result
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public DateTime? DueBy { get; init; }
        public bool IsComplete { get; init; }
    }

    public class GetRequest
    {
        public required string Id { get; init; }
    }

    [Test]
    public async Task CreateTodo_Success()
    {
        // Arrange
        const string expectedTitle = "Test Todo";

        // Post request and assert response
        var response = await Client.PostAsJsonAsync(Routes.Todos.Create, new Request { Title = expectedTitle });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Validate response data
        var result = await response.Content.ReadFromJsonAsync<Result>();
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Title).IsEqualTo(expectedTitle);

        // Validate location header
        var get = await Client.PostAsJsonAsync(Routes.Todos.GetById, new GetRequest { Id = result.Id });
        await Assert.That(get.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var getResult = await get.Content.ReadFromJsonAsync<Result>();
        await Assert.That(getResult).IsNotNull();
        await Assert.That(getResult.Title).IsEqualTo(expectedTitle);

        // Validate database entry
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
        var exists = await db.Todos.AsNoTracking().AnyAsync(t => t.Title == expectedTitle);
        await Assert.That(exists).IsTrue();
    }
}