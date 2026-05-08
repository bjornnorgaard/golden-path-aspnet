using FluentValidation;
using Platform.Annotations;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Endpoints;

namespace WebApi.Features.Todos;

[Endpoint(Routes.Todos.Create, EndpointMethod.Post)]
public class CreateTodo : IFeature<CreateTodo.Request, CreateTodo.Result, CreateTodo.Handler>
{
    public class Request
    {
        public required string Title { get; init; }
        public DateTime? DueBy { get; init; }
    }

    public class Result
    {
        public TodoId Id { get; init; }
        public required string Title { get; init; }
        public DateTime? DueBy { get; init; }
        public bool IsComplete { get; init; }
    }

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Title).NotEmpty().MinimumLength(3);
        }
    }

    [Service(lifetime: ServiceLifetime.Transient, asSelf: true)]
    public class Handler(TodoContext context)
    {
        public async Task<Result> Handle(Request req, CancellationToken ct)
        {
            var dbTodo = new Todo
            {
                Id = TodoId.New(),
                Title = req.Title,
                DueBy = req.DueBy,
                IsComplete = false
            };

            await context.Todos.AddAsync(dbTodo, ct);
            await context.SaveChangesAsync(ct);

            return new Result
            {
                Id = dbTodo.Id,
                Title = dbTodo.Title,
                DueBy = dbTodo.DueBy,
                IsComplete = dbTodo.IsComplete
            };
        }
    }
}