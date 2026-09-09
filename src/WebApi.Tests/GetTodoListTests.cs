using System.Net;
using System.Net.Http.Json;
using System.Text;
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

        // Act: skip the first five todos and return the next five
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.GetList, new GetTodoListApiRequest
        {
            Limit = 5,
            Offset = 5
        });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetTodoListResponse>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Todos.Count).IsEqualTo(5);
        await Assert.That(body.Todos.All(t => Guid.TryParse(t.Id, out _))).IsTrue();
    }

    [Test]
    public async Task GetTodoList_NullPaging_UsesDefaults()
    {
        // Arrange
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
            await db.Todos.AddRangeAsync(Enumerable.Range(1, 25).Select(i => new Todo
            {
                Id = TodoId.New(),
                Title = $"Default page {i:00}",
                IsComplete = false
            }));
            await db.SaveChangesAsync();
        }

        // Act
        var response = await Client.PostAsync(
            TestRoutes.Todos.GetList,
            new StringContent("""{"limit":null,"offset":null}""", Encoding.UTF8, "application/json"));

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetTodoListResponse>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Todos.Count).IsEqualTo(20);
    }

    [Test]
    public async Task GetTodoList_ScalarDefaultPayload_ReturnsAJsonResponse()
    {
        // Scalar submits the defaults advertised by the OpenAPI document as JSON.
        var response = await Client.PostAsync(
            TestRoutes.Todos.GetList,
            new StringContent("""{"limit":20,"offset":0}""", Encoding.UTF8, "application/json"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/json");
        var body = await response.Content.ReadFromJsonAsync<GetTodoListResponse>();
        await Assert.That(body).IsNotNull();
    }

    [Test]
    public async Task GetTodoList_LimitAboveConfiguredMaximum_IsCapped()
    {
        // Arrange
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
            await db.Todos.AddRangeAsync(Enumerable.Range(1, 120).Select(i => new Todo
            {
                Id = TodoId.New(),
                Title = $"Maximum page {i:000}",
                IsComplete = false
            }));
            await db.SaveChangesAsync();
        }

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.GetList, new GetTodoListApiRequest
        {
            Limit = 200,
            Offset = 0
        });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetTodoListResponse>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Todos.Count).IsEqualTo(100);
    }

    [Test]
    public async Task GetTodoList_InvalidOffset_ReturnsValidationProblem()
    {
        // Arrange
        var req = new GetTodoListApiRequest { Limit = 10, Offset = -1 };

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.GetList, req);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var errors = await response.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();
        await Assert.That(errors).IsNotNull();
        await Assert.That(errors!.ContainsKey("Offset")).IsTrue();
        await Assert.That(errors["Offset"].Any(message => message.Contains("greater than or equal to '0'"))).IsTrue();
    }

    [Test]
    public async Task GetTodoList_InvalidLimit_ReturnsValidationProblem()
    {
        // Arrange
        var req = new GetTodoListApiRequest { Limit = 0, Offset = 0 };

        // Act
        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.GetList, req);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var errors = await response.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();
        await Assert.That(errors).IsNotNull();
        await Assert.That(errors!.ContainsKey("Limit")).IsTrue();
        await Assert.That(errors["Limit"].Any(message => message.Contains("greater than or equal to '1'"))).IsTrue();
    }
}
