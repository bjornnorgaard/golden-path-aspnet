namespace WebApi.features.todos;

public class CreateTodo
{
    public record Request(string Title);

    public record Result(Guid id, string Title);

    public class Endpoint
    {
    }

    public class Handler
    {
    }
}