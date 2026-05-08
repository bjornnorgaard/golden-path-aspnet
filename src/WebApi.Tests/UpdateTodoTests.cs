using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Tests.Contracts;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

public class UpdateTodoTests : TestBase
{
    [Test]
    public async Task UpdateTodo_Success()
    {
        // Arrange: create via API
        var create = await Client.PostAsJsonAsync(TestRoutes.Todos.Create, new CreateTodoApiRequest
        {
            Title = "Todo to update",
            DueBy = null
        });
        await Assert.That(create.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var created = await create.Content.ReadFromJsonAsync<CreateTodoResponse>();
        await Assert.That(created).IsNotNull();

        var newDueBy = DateTime.UtcNow.AddDays(3);

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.Update, new UpdateTodoApiRequest
        {
            Id = created!.Id,
            Title = "Updated title",
            DueBy = newDueBy,
            IsComplete = true
        });

        // Assert: response
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<CreateTodoResponse>();
        await Assert.That(updated).IsNotNull();
        await Assert.That(updated!.Id).IsEqualTo(created.Id);
        await Assert.That(updated.Title).IsEqualTo("Updated title");
        await Assert.That(updated.IsComplete).IsTrue();
        await Assert.That(updated.DueBy).IsEqualTo(newDueBy);

        // Assert: DB state only
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
        var todo = await db.Todos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == TodoId.MustParse(created.Id));
        await Assert.That(todo).IsNotNull();
        await Assert.That(todo!.Title).IsEqualTo("Updated title");
        await Assert.That(todo.IsComplete).IsTrue();
        await Assert.That(todo.DueBy).IsEqualTo(newDueBy);
    }

    [Test]
    public async Task UpdateTodo_InvalidTitle_ReturnsValidationProblem()
    {
        // Arrange
        var req = new UpdateTodoApiRequest
        {
            Id = TodoId.New().ToString(),
            Title = "ab",
            DueBy = null,
            IsComplete = false
        };

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.Update, req);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("\"errors\"");
        await Assert.That(body).Contains("\"Title\"");
        await Assert.That(body).Contains("at least 3");
    }

    [Test]
    public async Task UpdateTodo_InvalidId_ReturnsValidationProblem()
    {
        // Arrange
        var req = new UpdateTodoApiRequest
        {
            Id = "not-a-guid",
            Title = "Valid Title",
            DueBy = null,
            IsComplete = false
        };

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.Update, req);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("\"errors\"");
        await Assert.That(body).Contains("\"Id\"");
    }

    [Test]
    public async Task UpdateTodo_NotFound_ReturnsNotFound()
    {
        // Arrange
        var req = new UpdateTodoApiRequest
        {
            Id = TodoId.New().ToString(),
            Title = "Updated title",
            DueBy = null,
            IsComplete = true
        };

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.Update, req);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Todo was not found");
    }
}