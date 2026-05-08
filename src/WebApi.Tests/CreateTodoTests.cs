using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Tests.Contracts;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

public class CreateTodoTests : TestBase
{
    [ClassDataSource<PostgresContainer>(Shared = SharedType.PerTestSession)]
    public PostgresContainer Postgres { get; init; } = null!;

    [Test]
    public async Task CreateTodo_Success()
    {
        // Arrange
        const string expectedTitle = "Test Todo";

        // POST request to create the item
        var createTodoRequest = new CreateTodoApiRequest { Title = expectedTitle, DueBy = null };
        var response = await Client.PostAsJsonAsync(Routes.Todos.Create, createTodoRequest);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Validate response from POST request
        var created = await response.Content.ReadFromJsonAsync<CreateTodoResponse>();
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.Title).IsEqualTo(expectedTitle);
        await Assert.That(Guid.TryParse(created.Id, out _)).IsTrue();

        // Validate item exists in the database
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
        var strongTodoId = TodoId.MustParse(created.Id);
        var exists = await db.Todos.AsNoTracking().AnyAsync(t => t.Id == strongTodoId);
        await Assert.That(exists).IsTrue();

        // GET request to fetch the created item
        var getTodoByIdRequest = new GetTodoByIdApiRequest { Id = created.Id };
        var get = await Client.PostAsJsonAsync(Routes.Todos.GetById, getTodoByIdRequest);
        await Assert.That(get.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Validate response from GET request
        var fetched = await get.Content.ReadFromJsonAsync<CreateTodoResponse>();
        await Assert.That(fetched).IsNotNull();
        await Assert.That(fetched!.Id).IsEqualTo(created.Id);
        await Assert.That(fetched.Title).IsEqualTo(expectedTitle);
    }
}