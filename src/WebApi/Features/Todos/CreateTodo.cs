using FluentValidation;
using Platform.Annotations;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Endpoints;

namespace WebApi.Features.Todos;

[Endpoint(Routes.Todos.Create, EndpointMethod.Post)]
public class CreateTodo
    : IFeature<CreateTodo.RequestBody, CreateTodo.ResponseBody, CreateTodo.Command, CreateTodo.Result, CreateTodo.Handler>
{
    public class RequestBody
    {
        public required string Title { get; init; }
        public DateTime? DueBy { get; init; }
    }

    public class ResponseBody
    {
        public TodoId Id { get; init; }
        public required string Title { get; init; }
        public DateTime? DueBy { get; init; }
        public bool IsComplete { get; init; }
    }

    public sealed class Validator : AbstractValidator<RequestBody>
    {
        public Validator()
        {
            RuleFor(x => x.Title).NotEmpty().MinimumLength(3);
        }
    }

    public class Command
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

    public static Command MapToCommand(RequestBody request) =>
        new()
        {
            Title = request.Title,
            DueBy = request.DueBy
        };

    public static ResponseBody MapToResponseBody(Result result) =>
        new()
        {
            Id = result.Id,
            Title = result.Title,
            DueBy = result.DueBy,
            IsComplete = result.IsComplete
        };

    [Service(lifetime: ServiceLifetime.Transient, asSelf: true)]
    public class Handler(TodoContext context)
    {
        public async Task<Result> Handle(Command cmd, CancellationToken ct)
        {
            var dbTodo = new Todo
            {
                Id = TodoId.New(),
                Title = cmd.Title,
                DueBy = cmd.DueBy,
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