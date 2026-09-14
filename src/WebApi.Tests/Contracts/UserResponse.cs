namespace WebApi.Tests.Contracts;

public sealed class UserResponse
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public string? GivenName { get; init; }
    public string? FamilyName { get; init; }
    public string? AvatarUrl { get; init; }
}
