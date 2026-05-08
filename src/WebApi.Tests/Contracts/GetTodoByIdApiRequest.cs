namespace WebApi.Tests.Contracts;

public sealed class GetTodoByIdApiRequest
{
    public required string Id { get; init; }
}