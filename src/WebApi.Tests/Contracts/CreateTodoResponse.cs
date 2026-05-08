namespace WebApi.Tests.Contracts;

public sealed class CreateTodoResponse
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public DateTime? DueBy { get; init; }
    public bool IsComplete { get; init; }
}