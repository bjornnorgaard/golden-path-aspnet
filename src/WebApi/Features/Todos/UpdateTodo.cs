using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platform.Annotations;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Endpoints;

namespace WebApi.Features.Todos;

[Endpoint(Routes.Todos.Update, EndpointMethod.Post)]
public class UpdateTodo
    : IFeature<UpdateTodo.RequestBody, UpdateTodo.ResponseBody, UpdateTodo.Command, UpdateTodo.Result, UpdateTodo.Handler>
{
    public class RequestBody
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public DateTime? DueBy { get; init; }
        public bool IsComplete { get; init; }
    }

    public class ResponseBody
    {
        public required TodoId Id { get; init; }
        public required string Title { get; init; }
        public DateTime? DueBy { get; init; }
        public bool IsComplete { get; init; }
    }

    // ReSharper disable once UnusedType.Global
    public sealed class Validator : AbstractValidator<RequestBody>
    {
        public Validator()
        {
            RuleFor(x => x.Id).NotEmpty().Must(id => TodoId.TryParse(id, out _));
            RuleFor(x => x.Title).NotEmpty().MinimumLength(3);
        }
    }

    public class Command
    {
        public required TodoId Id { get; init; }
        public required string Title { get; init; }
        public DateTime? DueBy { get; init; }
        public bool IsComplete { get; init; }
    }

    public class Result
    {
        public required TodoId Id { get; init; }
        public required string Title { get; init; }
        public DateTime? DueBy { get; init; }
        public bool IsComplete { get; init; }
    }

    public static Command MapToCommand(RequestBody request)
    {
        return new Command
        {
            Id = TodoId.MustParse(request.Id),
            Title = request.Title,
            DueBy = request.DueBy,
            IsComplete = request.IsComplete
        };
    }

    public static ResponseBody MapToResponseBody(Result result)
    {
        return new ResponseBody
        {
            Id = result.Id,
            Title = result.Title,
            DueBy = result.DueBy,
            IsComplete = result.IsComplete
        };
    }

    [Service(lifetime: ServiceLifetime.Transient)]
    public class Handler(TodoContext context)
    {
        public async Task<Outcome<Result>> Handle(Command cmd, CancellationToken ct)
        {
            var todo = await context.Todos.FirstOrDefaultAsync(t => t.Id == cmd.Id, ct);
            if (todo == null)
            {
                return Outcome<Result>.NotFound("Todo was not found.");
            }

            todo.Title = cmd.Title;
            todo.DueBy = cmd.DueBy;
            todo.IsComplete = cmd.IsComplete;
            await context.SaveChangesAsync(ct);

            return Outcome<Result>.Ok(new Result
            {
                Id = todo.Id,
                Title = todo.Title,
                DueBy = todo.DueBy,
                IsComplete = todo.IsComplete
            });
        }
    }
}