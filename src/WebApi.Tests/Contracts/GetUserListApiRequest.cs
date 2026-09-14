namespace WebApi.Tests.Contracts;

public sealed class GetUserListApiRequest
{
    public int? Limit { get; init; }
    public int? Offset { get; init; }
}
