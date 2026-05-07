namespace WebApi.Database.Models;

public class Todo
{
    public TodoId Id { get; set; }

    public required string Title { get; set; }

    public DateTime? DueBy { get; set; }

    public required bool IsComplete { get; set; }
}