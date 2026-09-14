namespace WebApi.Tests.Contracts;

public sealed class GetUserListResponse
{
    public required List<UserListItemResponse> Users { get; init; }
}

public sealed class UserListItemResponse
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public string? GivenName { get; init; }
    public string? FamilyName { get; init; }
    public string? AvatarUrl { get; init; }
}
