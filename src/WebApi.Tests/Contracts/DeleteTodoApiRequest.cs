namespace WebApi.Tests.Contracts;

public sealed class DeleteTodoApiRequest
{
    public required string Id { get; init; }
}