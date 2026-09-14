using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Tests.Contracts;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

public class DeleteUserTests : TestBase
{
    [Test]
    public async Task DeleteUser_Success()
    {
        // Arrange: seed a user directly in the DB
        var userId = UserId.New();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
            await db.Users.AddAsync(new User { Id = userId, Email = $"{userId}@example.com" });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Users.Delete, new DeleteUserApiRequest { Id = userId.ToString() });

        // Assert: response
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DeleteUserResponse>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Id).IsEqualTo(userId.ToString());

        // Assert: DB state
        await using var verifyScope = Factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TodoContext>();
        var exists = await verifyDb.Users.AsNoTracking().AnyAsync(u => u.Id == userId);
        await Assert.That(exists).IsFalse();
    }

    [Test]
    public async Task DeleteUser_CascadesTodos()
    {
        // Arrange: a user with one todo
        var userId = UserId.New();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
            await db.Users.AddAsync(new User { Id = userId, Email = $"{userId}@example.com" });
            await db.Todos.AddAsync(new Todo
            {
                Id = TodoId.New(),
                Title = "Owned by a user that is about to be deleted",
                IsComplete = false,
                OwnerUserId = userId
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Users.Delete, new DeleteUserApiRequest { Id = userId.ToString() });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        await using var verifyScope = Factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TodoContext>();
        var anyTodosLeft = await verifyDb.Todos.AsNoTracking().AnyAsync(t => t.OwnerUserId == userId);
        await Assert.That(anyTodosLeft).IsFalse();
    }

    [Test]
    public async Task DeleteUser_InvalidId_ReturnsValidationProblem()
    {
        // Arrange
        var req = new DeleteUserApiRequest { Id = "not-a-guid" };

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Users.Delete, req);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var errors = await response.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();
        await Assert.That(errors).IsNotNull();
        await Assert.That(errors!.ContainsKey("Id")).IsTrue();
    }

    [Test]
    public async Task DeleteUser_NotFound_ReturnsNotFound()
    {
        // Arrange
        var missing = UserId.New().ToString();

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Users.Delete, new DeleteUserApiRequest { Id = missing });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("User was not found");
    }
}
