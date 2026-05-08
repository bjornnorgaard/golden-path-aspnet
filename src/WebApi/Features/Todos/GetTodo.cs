using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platform.Annotations;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Endpoints;

namespace WebApi.Features.Todos;

[Endpoint(Routes.Todos.GetById, EndpointMethod.Post)]
public class GetTodo
    : IFeature<GetTodo.RequestBody, GetTodo.ResponseBody, GetTodo.Command, GetTodo.Result?, GetTodo.Handler>
{
    public class RequestBody
    {
        public required string Id { get; init; }
    }

    public class ResponseBody
    {
        public required TodoId Id { get; init; }
        public required string Title { get; init; }
        public DateTime? DueBy { get; init; }
        public bool IsComplete { get; init; }
    }

    public sealed class Validator : AbstractValidator<RequestBody>
    {
        public Validator()
        {
            RuleFor(x => x.Id).NotEmpty().Must(id => TodoId.TryParse(id, out _));
        }
    }

    public class Command
    {
        public required TodoId Id { get; init; }
    }

    public class Result
    {
        public required TodoId Id { get; init; }
        public required string Title { get; init; }
        public DateTime? DueBy { get; init; }
        public bool IsComplete { get; init; }
    }

    public static Command MapToCommand(RequestBody request) =>
        new()
        {
            // Validator guarantees this parses; handler owns mapped types.
            Id = TodoId.MustParse(request.Id)
        };

    public static ResponseBody MapToResponseBody(Result? result) =>
        // Generator guards null => NotFound; this is here to satisfy the feature contract.
        result == null
            ? throw new ArgumentNullException(nameof(result))
            : new ResponseBody
            {
                Id = result.Id,
                Title = result.Title,
                DueBy = result.DueBy,
                IsComplete = result.IsComplete
            };

    [Service(lifetime: ServiceLifetime.Transient, asSelf: true)]
    public class Handler(TodoContext context)
    {
        public async Task<Result?> Handle(Command cmd, CancellationToken ct)
        {
            var todo = await context.Todos.FirstOrDefaultAsync(t => t.Id == cmd.Id, ct);
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