using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Tests.Contracts;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

public class UpdateUserTests : TestBase
{
    [Test]
    public async Task UpdateUser_Success()
    {
        // Arrange: seed a user directly in the DB
        var userId = UserId.New();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
            await db.Users.AddAsync(new User
            {
                Id = userId,
                Email = $"{userId}@example.com",
                DisplayName = "Old Name"
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Users.Update, new UpdateUserApiRequest
        {
            Id = userId.ToString(),
            DisplayName = "New Name",
            GivenName = "New",
            FamilyName = "Name",
            AvatarUrl = "https://example.com/new.png"
        });

        // Assert: response
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Id).IsEqualTo(userId.ToString());
        await Assert.That(body.DisplayName).IsEqualTo("New Name");
        await Assert.That(body.GivenName).IsEqualTo("New");
        await Assert.That(body.FamilyName).IsEqualTo("Name");
        await Assert.That(body.AvatarUrl).IsEqualTo("https://example.com/new.png");

        // Assert: DB state
        await using var verifyScope = Factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TodoContext>();
        var stored = await verifyDb.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        await Assert.That(stored.DisplayName).IsEqualTo("New Name");
    }

    [Test]
    public async Task UpdateUser_InvalidId_ReturnsValidationProblem()
    {
        // Arrange
        var req = new UpdateUserApiRequest { Id = "not-a-guid", DisplayName = "Someone" };

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Users.Update, req);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var errors = await response.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();
        await Assert.That(errors).IsNotNull();
        await Assert.That(errors!.ContainsKey("Id")).IsTrue();
    }

    [Test]
    public async Task UpdateUser_NotFound_ReturnsNotFound()
    {
        // Arrange
        var missing = UserId.New().ToString();

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Users.Update, new UpdateUserApiRequest { Id = missing, DisplayName = "Nobody" });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("User was not found");
    }
}
