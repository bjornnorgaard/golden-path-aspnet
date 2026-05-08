using System.Diagnostics;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platform.Annotations;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Endpoints;
using WebApi.Telemetry;

namespace WebApi.Features.Todos;

[Endpoint(Routes.Todos.GetById, EndpointMethod.Post)]
public class GetTodoById
    : IFeature<GetTodoById.RequestBody, GetTodoById.ResponseBody, GetTodoById.Command, GetTodoById.Result, GetTodoById.Handler>
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

    // ReSharper disable once UnusedType.Global
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

    public static Command MapToCommand(RequestBody request)
    {
        return new Command
        {
            Id = TodoId.MustParse(request.Id)
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
            Activity.Current?.SetTodoId(cmd.Id);

            var todo = await context.Todos.FirstOrDefaultAsync(t => t.Id == cmd.Id, ct);
            if (todo == null)
            {
                return Outcome<Result>.NotFound("Todo was not found.");
            }

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