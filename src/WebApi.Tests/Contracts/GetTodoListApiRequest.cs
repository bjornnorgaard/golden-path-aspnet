namespace WebApi.Tests.Contracts;

public sealed class GetTodoListApiRequest
{
    public int? Limit { get; init; }
    public int? Offset { get; init; }
}
