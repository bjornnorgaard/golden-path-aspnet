using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Telemetry;
using WebApi.Users.Contracts;
using WebApi.Users.Endpoints;
using UserId = WebApi.Database.Models.UserId;

namespace WebApi.Features.Users.DeleteUser;

internal sealed class DeleteUserEndpoint(DeleteUserHandler handler) : IDeleteUserEndpoint
{
    public async Task<Results<Ok<DeleteUserResponse>, BadRequest<IReadOnlyDictionary<string, string[]>>, NotFound<string>>> HandleAsync(
        DeleteUserRequest request,
        CancellationToken ct)
    {
        if (!UserId.TryParse(request.Id, out var userId))
        {
            return TypedResults.BadRequestUserIdInvalid();
        }

        Activity.Current?.SetUserId(userId);

        var result = await handler.HandleAsync(new DeleteUserHandler.Command { Id = userId }, ct);
        if (result is null)
        {
            return TypedResults.NotFound("User was not found.");
        }

        return TypedResults.Ok(new DeleteUserResponse { Id = result.Id.Value });
    }
}
