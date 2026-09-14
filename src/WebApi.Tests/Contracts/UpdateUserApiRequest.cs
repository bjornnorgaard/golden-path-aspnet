namespace WebApi.Tests.Contracts;

public sealed class UpdateUserApiRequest
{
    public required string Id { get; init; }
    public string? DisplayName { get; init; }
    public string? GivenName { get; init; }
    public string? FamilyName { get; init; }
    public string? AvatarUrl { get; init; }
}
