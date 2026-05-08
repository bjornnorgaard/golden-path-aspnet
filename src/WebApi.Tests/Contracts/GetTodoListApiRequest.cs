namespace WebApi.Tests.Contracts;

public sealed class GetTodoListApiRequest
{
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}