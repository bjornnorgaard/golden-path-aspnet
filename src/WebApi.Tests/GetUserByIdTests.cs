using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Tests.Contracts;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

public class GetUserByIdTests : TestBase
{
    [Test]
    public async Task GetUserById_Success()
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
                DisplayName = "Ada Lovelace",
                GivenName = "Ada",
                FamilyName = "Lovelace",
                AvatarUrl = "https://example.com/ada.png"
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Users.GetById, new GetUserByIdApiRequest { Id = userId.ToString() });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Id).IsEqualTo(userId.ToString());
        await Assert.That(body.Email).IsEqualTo($"{userId}@example.com");
        await Assert.That(body.DisplayName).IsEqualTo("Ada Lovelace");
        await Assert.That(body.GivenName).IsEqualTo("Ada");
        await Assert.That(body.FamilyName).IsEqualTo("Lovelace");
        await Assert.That(body.AvatarUrl).IsEqualTo("https://example.com/ada.png");
    }

    [Test]
    public async Task GetUserById_InvalidId_ReturnsValidationProblem()
    {
        // Arrange
        var req = new GetUserByIdApiRequest { Id = "not-a-guid" };

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Users.GetById, req);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var errors = await response.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();
        await Assert.That(errors).IsNotNull();
        await Assert.That(errors!.ContainsKey("Id")).IsTrue();
        await Assert.That(errors["Id"].Any(message => message.Contains("correct format"))).IsTrue();
    }

    [Test]
    public async Task GetUserById_NotFound_ReturnsNotFound()
    {
        // Arrange
        var missing = UserId.New().ToString();

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Users.GetById, new GetUserByIdApiRequest { Id = missing });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("User was not found");
    }
}
