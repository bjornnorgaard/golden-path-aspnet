using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Tests.Contracts;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

public class DeleteTodoTests : TestBase
{
    [Test]
    public async Task DeleteTodo_Success()
    {
        // Arrange: create via API
        var create = await Client.PostAsJsonAsync(TestRoutes.Todos.Create, new CreateTodoApiRequest
        {
            Title = "Todo to delete",
            DueBy = null
        });
        await Assert.That(create.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var created = await create.Content.ReadFromJsonAsync<CreateTodoResponse>();
        await Assert.That(created).IsNotNull();

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.Delete, new DeleteTodoApiRequest { Id = created!.Id });

        // Assert: response
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DeleteTodoResponse>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Id).IsEqualTo(created.Id);

        // Assert: DB state only
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
        var exists = await db.Todos.AsNoTracking().AnyAsync(t => t.Id == TodoId.MustParse(created.Id));
        await Assert.That(exists).IsFalse();
    }

    [Test]
    public async Task DeleteTodo_InvalidId_ReturnsValidationProblem()
    {
        // Arrange
        var req = new DeleteTodoApiRequest { Id = "not-a-guid" };

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.Delete, req);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("\"errors\"");
        await Assert.That(body).Contains("\"Id\"");
    }

    [Test]
    public async Task DeleteTodo_NotFound_ReturnsNotFound()
    {
        // Arrange
        var missing = TodoId.New().ToString();

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.Delete, new DeleteTodoApiRequest { Id = missing });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Todo was not found");
    }
}