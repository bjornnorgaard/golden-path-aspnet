using System.Diagnostics;
using FluentValidation;
using Goldenpath.Todos.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using WebApi.Features.Todos.CreateTodo;
using WebApi.Features.Todos.DeleteTodo;
using WebApi.Features.Todos.GetTodoById;
using WebApi.Features.Todos.GetTodoList;
using WebApi.Features.Todos.ToggleTodo;
using WebApi.Features.Todos.UpdateTodo;
using WebApi.Services;
using WebApi.Telemetry;
using Rest = WebApi.Todos.Contracts;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos;

/// <summary>
/// The gRPC adapter over the todo handlers (Contracts/Grpc/goldenpath/todos/v1/todos.proto). Like the
/// REST endpoints and GraphQL resolvers it maps types and contains no business rules: input rules come
/// from the validators generated out of the OpenAPI contract, so every transport enforces the same ones.
/// Every RPC in the contract must be overridden here; a test fails if one is missing.
/// </summary>
internal sealed class TodosGrpcService(
    CreateTodoHandler create,
    GetTodoByIdHandler get,
    GetTodoListHandler list,
    UpdateTodoHandler update,
    ToggleTodoHandler toggle,
    DeleteTodoHandler delete,
    IValidator<Rest.CreateTodoRequest> createValidator,
    IValidator<Rest.UpdateTodoRequest> updateValidator,
    IValidator<Rest.GetTodoListRequest> listValidator) : TodosService.TodosServiceBase
{
    public override async Task<CreateTodoResponse> CreateTodo(CreateTodoRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var rest = new Rest.CreateTodoRequest { Title = request.Title, DueBy = request.DueBy?.ToDateTimeOffset() };
        await ValidateAsync(createValidator, rest, ct);
        var dueBy = rest.DueBy.ToUtcDueBy();

        var result = await create.HandleAsync(new CreateTodoHandler.Command { Title = request.Title, DueBy = dueBy }, ct);
        Activity.Current?.SetTodoId(result.Id);

        return new CreateTodoResponse
        {
            Todo = ToTodo(result.Id.Value, request.Title, dueBy, isComplete: false)
        };
    }

    public override async Task<GetTodoByIdResponse> GetTodoById(GetTodoByIdRequest request, ServerCallContext context)
    {
        var todoId = ParseId(request.Id);
        var result = await get.HandleAsync(new GetTodoByIdHandler.Command { Id = todoId }, context.CancellationToken)
                     ?? throw TodoNotFound();

        return new GetTodoByIdResponse { Todo = ToTodo(result.Id.Value, result.Title, result.DueBy, result.IsComplete) };
    }

    public override async Task<GetTodoListResponse> GetTodoList(GetTodoListRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        int? limit = request.HasLimit ? request.Limit : null;
        int? offset = request.HasOffset ? request.Offset : null;
        await ValidateAsync(listValidator, new Rest.GetTodoListRequest { Limit = limit, Offset = offset }, ct);

        var result = await list.HandleAsync(new GetTodoListHandler.Command { Limit = limit, Offset = offset }, ct);

        var response = new GetTodoListResponse();
        response.Todos.AddRange(result.Todos.Select(todo => ToTodo(todo.Id, todo.Title, todo.DueBy?.UtcDateTime, todo.IsComplete)));
        return response;
    }

    public override async Task<UpdateTodoResponse> UpdateTodo(UpdateTodoRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var todoId = ParseId(request.Id);
        var rest = new Rest.UpdateTodoRequest
        {
            Id = request.Id,
            Title = request.Title,
            DueBy = request.DueBy?.ToDateTimeOffset(),
            IsComplete = request.IsComplete
        };
        await ValidateAsync(updateValidator, rest, ct);

        var result = await update.HandleAsync(new UpdateTodoHandler.Command
        {
            Id = todoId,
            Title = request.Title,
            DueBy = rest.DueBy.ToUtcDueBy(),
            IsComplete = request.IsComplete
        }, ct) ?? throw TodoNotFound();

        return new UpdateTodoResponse { Todo = ToTodo(result.Id.Value, result.Title, result.DueBy, result.IsComplete) };
    }

    public override async Task<ToggleTodoResponse> ToggleTodo(ToggleTodoRequest request, ServerCallContext context)
    {
        var todoId = ParseId(request.Id);
        var result = await toggle.HandleAsync(new ToggleTodoHandler.Command { Id = todoId }, context.CancellationToken)
                     ?? throw TodoNotFound();

        return new ToggleTodoResponse { Todo = ToTodo(result.Id.Value, result.Title, result.DueBy, result.IsComplete) };
    }

    public override async Task<DeleteTodoResponse> DeleteTodo(DeleteTodoRequest request, ServerCallContext context)
    {
        var todoId = ParseId(request.Id);
        var result = await delete.HandleAsync(new DeleteTodoHandler.Command { Id = todoId }, context.CancellationToken)
                     ?? throw TodoNotFound();

        return new DeleteTodoResponse { Id = result.Id.Value.ToString() };
    }

    private static TodoId ParseId(string id)
    {
        if (!TodoId.TryParse(id, out var todoId))
        {
            throw GrpcErrors.InvalidId();
        }

        Activity.Current?.SetTodoId(todoId);
        return todoId;
    }

    private static async Task ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            throw GrpcErrors.InvalidArgument(validation);
        }
    }

    private static RpcException TodoNotFound() => GrpcErrors.NotFound("Todo was not found.");

    private static Todo ToTodo(Guid id, string title, DateTime? dueBy, bool isComplete)
    {
        var todo = new Todo { Id = id.ToString(), Title = title, IsComplete = isComplete };
        if (dueBy is { } due)
        {
            // timestamptz comes back Utc-kinded; anything else is treated as already being UTC.
            todo.DueBy = Timestamp.FromDateTime(due.Kind == DateTimeKind.Utc ? due : DateTime.SpecifyKind(due, DateTimeKind.Utc));
        }

        return todo;
    }
}
