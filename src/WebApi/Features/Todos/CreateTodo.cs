using Platform.Annotations;

namespace WebApi.features.todos;

public class CreateTodo
{
    public record Request(string Title);

    public record Result(TodoId id, string Title);

    [Service(lifetime: ServiceLifetime.Transient, asSelf: true)]
    public class Handler
    {
        public Task<Result> Handle(Request request, CancellationToken ct)
        {
            return Task.FromResult(new Result(TodoId.New(), request.Title));
        }
    }
}