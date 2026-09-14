using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Telemetry;
using WebApi.Users.Contracts;
using WebApi.Users.Endpoints;
using UserId = WebApi.Database.Models.UserId;

namespace WebApi.Features.Users.GetUserById;

internal sealed class GetUserByIdEndpoint(GetUserByIdHandler handler) : IGetUserByIdEndpoint
{
    public async Task<Results<Ok<GetUserByIdResponse>, BadRequest<IReadOnlyDictionary<string, string[]>>, NotFound<string>>> HandleAsync(
        GetUserByIdRequest request,
        CancellationToken ct)
    {
        if (!UserId.TryParse(request.Id, out var userId))
        {
            return TypedResults.BadRequestUserIdInvalid();
        }

        Activity.Current?.SetUserId(userId);

        var result = await handler.HandleAsync(new GetUserByIdHandler.Command { Id = userId }, ct);
        if (result is null)
        {
            return TypedResults.NotFound("User was not found.");
        }

        return TypedResults.Ok(new GetUserByIdResponse
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
