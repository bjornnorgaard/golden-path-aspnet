using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

public sealed class GraphQlTests : TestBase
{
    [Test]
    public async Task Playground_is_available_in_development()
    {
        var response = await Client.GetAsync("/graphql/playground/");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Schema_exposes_every_operation_from_the_openapi_contract()
    {
        var result = await ExecuteAsync("{ __schema { queryType { fields { name } } mutationType { fields { name } } } }");
        var schema = result.RootElement.GetProperty("data").GetProperty("__schema");
        var queries = schema.GetProperty("queryType").GetProperty("fields").EnumerateArray().Select(field => field.GetProperty("name").GetString()).ToArray();
        var mutations = schema.GetProperty("mutationType").GetProperty("fields").EnumerateArray().Select(field => field.GetProperty("name").GetString()).ToArray();

        await Assert.That(queries).Contains("getTodoById");
        await Assert.That(queries).Contains("getTodoList");
        await Assert.That(mutations).Contains("createTodo");
        await Assert.That(mutations).Contains("updateTodo");
        await Assert.That(mutations).Contains("toggleTodo");
        await Assert.That(mutations).Contains("deleteTodo");
    }

    [Test]
    public async Task Schema_exposes_every_operation_from_the_users_openapi_contract()
    {
        var result = await ExecuteAsync("{ __schema { queryType { fields { name } } mutationType { fields { name } } } }");
        var schema = result.RootElement.GetProperty("data").GetProperty("__schema");
        var queries = schema.GetProperty("queryType").GetProperty("fields").EnumerateArray().Select(field => field.GetProperty("name").GetString()).ToArray();
        var mutations = schema.GetProperty("mutationType").GetProperty("fields").EnumerateArray().Select(field => field.GetProperty("name").GetString()).ToArray();

        await Assert.That(queries).Contains("getUserById");
        await Assert.That(queries).Contains("getUserList");
        await Assert.That(mutations).Contains("updateUser");
        await Assert.That(mutations).Contains("deleteUser");
    }

    [Test]
    public async Task UpdateUser_and_getUserById_use_existing_endpoint_handlers()
    {
        var userId = UserId.New();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
            await db.Users.AddAsync(new User { Id = userId, Email = $"{userId}@example.com", DisplayName = "Old Name" });
            await db.SaveChangesAsync();
        }

        var update = await ExecuteAsync("""
                                        mutation {
                                          updateUser(input: { id: "%ID%", displayName: "Updated Through GraphQL" }) {
                                            id
                                            displayName
                                          }
                                        }
                                        """.Replace("%ID%", userId.ToString()));

        await Assert.That(update.RootElement.TryGetProperty("errors", out _)).IsFalse();
        var updated = update.RootElement.GetProperty("data").GetProperty("updateUser");
        await Assert.That(updated.GetProperty("displayName").GetString()).IsEqualTo("Updated Through GraphQL");

        var get = await ExecuteAsync("query { getUserById(input: { id: \"" + userId + "\" }) { id displayName } }");

        await Assert.That(get.RootElement.TryGetProperty("errors", out _)).IsFalse();
        var fetched = get.RootElement.GetProperty("data").GetProperty("getUserById");
        await Assert.That(fetched.GetProperty("id").GetString()).IsEqualTo(userId.ToString());
        await Assert.That(fetched.GetProperty("displayName").GetString()).IsEqualTo("Updated Through GraphQL");
    }

    [Test]
    public async Task DeleteUser_missing_id_returns_a_graphql_error()
    {
        var missing = UserId.New();
        var result = await ExecuteAsync("mutation { deleteUser(input: { id: \"" + missing + "\" }) { id } }");

        await Assert.That(result.RootElement.TryGetProperty("data", out var data)).IsTrue();
        await Assert.That(data.ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(result.RootElement.TryGetProperty("errors", out var errors)).IsTrue();
        await Assert.That(errors[0].GetProperty("message").GetString()).Contains("User was not found");
    }

    [Test]
    public async Task CreateTodo_and_getTodoById_use_existing_endpoint_handlers()
    {
        const string title = "A todo created through the generated GraphQL transport";
        var create = await ExecuteAsync("""
                                        mutation {
                                          createTodo(input: { title: "A todo created through the generated GraphQL transport" }) {
                                            id
                                            title
                                            isComplete
                                          }
                                        }
                                        """);

        await Assert.That(create.RootElement.TryGetProperty("errors", out _)).IsFalse();
        var created = create.RootElement.GetProperty("data").GetProperty("createTodo");
        var id = created.GetProperty("id").GetString();
        await Assert.That(id).IsNotNull();
        await Assert.That(created.GetProperty("title").GetString()).IsEqualTo(title);
        await Assert.That(created.GetProperty("isComplete").GetBoolean()).IsFalse();

        var get = await ExecuteAsync("query { getTodoById(input: { id: \"" + id + "\" }) { id title } }");

        await Assert.That(get.RootElement.TryGetProperty("errors", out _)).IsFalse();
        var fetched = get.RootElement.GetProperty("data").GetProperty("getTodoById");
        await Assert.That(fetched.GetProperty("id").GetString()).IsEqualTo(id);
        await Assert.That(fetched.GetProperty("title").GetString()).IsEqualTo(title);
    }

    [Test]
    public async Task CreateTodo_invalid_input_returns_a_graphql_error()
    {
        var result = await ExecuteAsync("""
                                        mutation {
                                          createTodo(input: { title: "ab" }) {
                                            id
                                          }
                                        }
                                        """);

        await Assert.That(result.RootElement.TryGetProperty("data", out var data)).IsTrue();
        await Assert.That(data.ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(result.RootElement.TryGetProperty("errors", out var errors)).IsTrue();
        await Assert.That(errors[0].GetProperty("message").GetString()).Contains("Title");
        await Assert.That(errors[0].GetProperty("extensions").GetProperty("traceId").GetString()).IsNotNullOrEmpty();
    }

    private async Task<JsonDocument> ExecuteAsync(string query)
    {
        var response = await Client.PostAsJsonAsync("/graphql", new { query });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
    }
}