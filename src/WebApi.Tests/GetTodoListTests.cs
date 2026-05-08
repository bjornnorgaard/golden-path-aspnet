using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Tests.Contracts;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

public class GetTodoListTests : TestBase
{
    [Test]
    public async Task GetTodoList_Paging_Success()
    {
        // Arrange: seed DB directly for predictable paging
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TodoContext>();

            var todos = Enumerable.Range(1, 12)
                .Select(i => new Todo
                {
                    Id = TodoId.New(),
                    Title = $"Todo {i:00}",
                    DueBy = null,
                    IsComplete = false
                })
                .ToArray();

            await db.Todos.AddRangeAsync(todos);
            await db.SaveChangesAsync();
        }

        // Act: page 2 with size 5 => items 6-10 in our title ordering
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.GetList, new GetTodoListApiRequest
        {
            Page = 2,
            PageSize = 5
        });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetTodoListResponse>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Todos.Count).IsEqualTo(5);
        await Assert.That(body.Todos.All(t => Guid.TryParse(t.Id, out _))).IsTrue();
    }

    [Test]
    public async Task GetTodoList_DefaultPaging_Success()
    {
        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.GetList, new GetTodoListApiRequest());

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetTodoListResponse>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Todos).IsNotNull();
    }

    [Test]
    public async Task GetTodoList_InvalidPage_ReturnsValidationProblem()
    {
        // Arrange
        var req = new GetTodoListApiRequest { Page = 0, PageSize = 10 };

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.GetList, req);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("\"errors\"");
        await Assert.That(body).Contains("\"Page\"");
    }

    [Test]
    public async Task GetTodoList_InvalidPageSize_ReturnsValidationProblem()
    {
        // Arrange
        var req = new GetTodoListApiRequest { Page = 1, PageSize = 0 };

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.GetList, req);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("\"errors\"");
        await Assert.That(body).Contains("\"PageSize\"");
    }
}