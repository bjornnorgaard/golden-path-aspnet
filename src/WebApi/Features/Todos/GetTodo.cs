using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platform.Annotations;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Endpoints;

namespace WebApi.Features.Todos;

[Endpoint(Routes.Todos.GetById, EndpointMethod.Post)]
public class GetTodo : IFeature<GetTodo.Request, GetTodo.Result, GetTodo.Handler>
{
    public class Request
    {
        public required string Id { get; init; }
    }

    public class Result
    {
        public required TodoId Id { get; init; }
        public required string Title { get; init; }
        public DateTime? DueBy { get; init; }
        public bool IsComplete { get; init; }
    }

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Id).NotEmpty().Must(id => TodoId.TryParse(id, out _));
        }
    }

    [Service(lifetime: ServiceLifetime.Transient, asSelf: true)]
    public class Handler(TodoContext context)
    {
        public async Task<Result?> Handle(Request req, CancellationToken ct)
        {
            // Validator guarantees this parses; handler owns mapping.
            var parsedId = TodoId.MustParse(req.Id);

            var todo = await context.Todos.FirstOrDefaultAsync(t => t.Id == parsedId, ct);
            if (todo == null) return null;

            return new Result
            {
                Id = todo.Id,
                Title = todo.Title,
                DueBy = todo.DueBy,
                IsComplete = todo.IsComplete
            };
        }
    }
}