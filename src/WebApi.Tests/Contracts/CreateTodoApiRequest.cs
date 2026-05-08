namespace WebApi.Tests.Contracts;

public sealed class CreateTodoApiRequest
{
    public required string Title { get; init; }
    public DateTime? DueBy { get; init; }
}