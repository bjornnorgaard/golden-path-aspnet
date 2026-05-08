namespace WebApi.Tests.Contracts;

public sealed class UpdateTodoApiRequest
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public DateTime? DueBy { get; init; }
    public bool IsComplete { get; init; }
}