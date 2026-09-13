namespace WebApi.Database.Models;

public class User
{
    public UserId Id { get; set; }

    public required string Email { get; set; }

    public string? DisplayName { get; set; }

    public string? GivenName { get; set; }

    public string? FamilyName { get; set; }

    public string? AvatarUrl { get; set; }

    public List<Todo> Todos { get; set; } = [];
}
