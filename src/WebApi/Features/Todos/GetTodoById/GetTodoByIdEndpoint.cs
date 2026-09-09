using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Telemetry;
using WebApi.Todos.Contracts;
using WebApi.Todos.Endpoints;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.GetTodoById;

internal sealed class GetTodoByIdEndpoint(GetTodoByIdHandler handler) : IGetTodoByIdEndpoint
{
    public async Task<Results<Ok<GetTodoByIdResponse>, BadRequest<string>, NotFound<string>>> HandleAsync(
        GetTodoByIdRequest request,
        CancellationToken ct)
    {
        if (!TodoId.TryParse(request.Id, out var todoId))
        {
            return TypedResults.BadRequest("Id must be a valid UUID.");
        }

        Activity.Current?.SetTodoId(todoId);

        var result = await handler.HandleAsync(new GetTodoByIdHandler.Command { Id = todoId }, ct);
        if (result is null)
        {
            return TypedResults.NotFound("Todo was not found.");
        }

        return TypedResults.Ok(new GetTodoByIdResponse
        {
            Id = result.Id.Value,
            Title = result.Title,
            DueBy = result.DueBy,
            IsComplete = result.IsComplete
        });
    }
}
