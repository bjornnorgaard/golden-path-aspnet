using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Tests.Contracts;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

public class GetUserListTests : TestBase
{
    [Test]
    public async Task GetUserList_Paging_Success()
    {
        // Arrange: seed enough users for a full page regardless of what earlier tests left behind
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
            var users = Enumerable.Range(1, 12)
                .Select(i =>
                {
                    var id = UserId.New();
                    return new User { Id = id, Email = $"{id}@example.com" };
                })
                .ToArray();

            await db.Users.AddRangeAsync(users);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Users.GetList, new GetUserListApiRequest
        {
            Limit = 5,
            Offset = 0
        });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetUserListResponse>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Users.Count).IsEqualTo(5);
        await Assert.That(body.Users.All(u => Guid.TryParse(u.Id, out _))).IsTrue();
    }

    [Test]
    public async Task GetUserList_LimitAboveConfiguredMaximum_IsCapped()
    {
        // Arrange
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
            var users = Enumerable.Range(1, 120)
                .Select(i =>
                {
                    var id = UserId.New();
                    return new User { Id = id, Email = $"{id}@example.com" };
                })
                .ToArray();

            await db.Users.AddRangeAsync(users);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Users.GetList, new GetUserListApiRequest
        {
            Limit = 200,
            Offset = 0
        });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetUserListResponse>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Users.Count).IsEqualTo(100);
    }

    [Test]
    public async Task GetUserList_InvalidOffset_ReturnsValidationProblem()
    {
        // Arrange
        var req = new GetUserListApiRequest { Limit = 10, Offset = -1 };

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Users.GetList, req);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var errors = await response.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();
        await Assert.That(errors).IsNotNull();
        await Assert.That(errors!.ContainsKey("Offset")).IsTrue();
    }
}
