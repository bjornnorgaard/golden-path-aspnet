using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platform.Annotations;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Endpoints;

namespace WebApi.Features.Todos;

[Endpoint(Routes.Todos.Delete, EndpointMethod.Post)]
public class DeleteTodo
    : IFeature<DeleteTodo.RequestBody, DeleteTodo.ResponseBody, DeleteTodo.Command, DeleteTodo.Result, DeleteTodo.Handler>
{
    public class RequestBody
    {
        public required string Id { get; init; }
    }

    public class ResponseBody
    {
        public required TodoId Id { get; init; }
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
            Id = result.Id
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

            context.Todos.Remove(todo);
            await context.SaveChangesAsync(ct);

            return Outcome<Result>.Ok(new Result { Id = cmd.Id });
        }
    }
}