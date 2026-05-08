namespace WebApi.Tests.Contracts;

public sealed class GetTodoListResponse
{
    public required List<TodoListItemResponse> Todos { get; init; }
}

public sealed class TodoListItemResponse
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public DateTime? DueBy { get; init; }
    public bool IsComplete { get; init; }
}