using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Telemetry;
using WebApi.Users.Contracts;
using WebApi.Users.Endpoints;
using UserId = WebApi.Database.Models.UserId;

namespace WebApi.Features.Users.UpdateUser;

internal sealed class UpdateUserEndpoint(UpdateUserHandler handler) : IUpdateUserEndpoint
{
    public async Task<Results<Ok<UpdateUserResponse>, BadRequest<IReadOnlyDictionary<string, string[]>>, NotFound<string>>> HandleAsync(
        UpdateUserRequest request,
        CancellationToken ct)
    {
        if (!UserId.TryParse(request.Id, out var userId))
        {
            return TypedResults.BadRequestUserIdInvalid();
        }

        Activity.Current?.SetUserId(userId);

        var result = await handler.HandleAsync(new UpdateUserHandler.Command
        {
            Id = userId,
            DisplayName = request.DisplayName,
            GivenName = request.GivenName,
            FamilyName = request.FamilyName,
            AvatarUrl = request.AvatarUrl
        }, ct);
        if (result is null)
        {
            return TypedResults.NotFound("User was not found.");
        }

        return TypedResults.Ok(new UpdateUserResponse
        {
            Id = result.Id.Value,
            Email = result.Email,
            DisplayName = result.DisplayName,
            GivenName = result.GivenName,
            FamilyName = result.FamilyName,
            AvatarUrl = result.AvatarUrl
        });
    }
}
