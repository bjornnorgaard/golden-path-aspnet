using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platform.Annotations;
using WebApi.Database;
using WebApi.Database.Models;
using WebApi.Endpoints;

namespace WebApi.Features.Todos;

[Endpoint(Routes.Todos.GetList, EndpointMethod.Post)]
public class GetTodoList
    : IFeature<GetTodoList.RequestBody, GetTodoList.ResponseBody, GetTodoList.Command, GetTodoList.Result, GetTodoList.Handler>
{
    public sealed class RequestBody
    {
        public int? Page { get; init; }
        public int? PageSize { get; init; }
    }

    public sealed class ResponseBody
    {
        public required IReadOnlyList<TodoItem> Todos { get; init; }
    }

    public sealed class TodoItem
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
            RuleFor(x => x.Page).GreaterThanOrEqualTo(1).When(x => x.Page.HasValue);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 200).When(x => x.PageSize.HasValue);
        }
    }

    public sealed class Command
    {
        public int Page { get; init; }
        public int PageSize { get; init; }
    }

    public sealed class Result
    {
        public required IReadOnlyList<TodoItem> Todos { get; init; }
    }

    public static Command MapToCommand(RequestBody request)
    {
        return new Command
        {
            Page = request.Page ?? 1,
            PageSize = request.PageSize ?? 50
        };
    }

    public static ResponseBody MapToResponseBody(Result result)
    {
        return new ResponseBody
        {
            Todos = result.Todos
        };
    }

    [Service(lifetime: ServiceLifetime.Transient)]
    public sealed class Handler(TodoContext context)
    {
        public async Task<Outcome<Result>> Handle(Command cmd, CancellationToken ct)
        {
            var skip = (cmd.Page - 1) * cmd.PageSize;
            var todos = await context.Todos
                .AsNoTracking()
                .OrderBy(t => t.Title)
                .ThenBy(t => t.Id)
                .Skip(skip)
                .Take(cmd.PageSize)
                .Select(t => new TodoItem
                {
                    Id = t.Id,
                    Title = t.Title,
                    DueBy = t.DueBy,
                    IsComplete = t.IsComplete
                })
                .ToListAsync(ct);

            return Outcome<Result>.Ok(new Result { Todos = todos });
        }
    }
}