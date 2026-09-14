using System.Diagnostics;
using WebApi.Telemetry;
using WebApi.Users.Contracts;
using WebApi.Users.GraphQl;
using UserId = WebApi.Database.Models.UserId;

namespace WebApi.Features.Users.UpdateUser;

internal sealed class UpdateUserResolver(UpdateUserHandler handler) : IUpdateUserResolver
{
    public async Task<UpdateUserResponse> ResolveAsync(UpdateUserRequest input, CancellationToken ct)
    {
        var userId = UserId.MustParse(input.Id);
        Activity.Current?.SetUserId(userId);

        var result = await handler.HandleAsync(new UpdateUserHandler.Command
        {
            Id = userId,
            DisplayName = input.DisplayName,
            GivenName = input.GivenName,
            FamilyName = input.FamilyName,
            AvatarUrl = input.AvatarUrl
        }, ct);
        if (result is null)
        {
            throw new HotChocolate.GraphQLException("User was not found.");
        }

        return new UpdateUserResponse
        {
            Id = result.Id.Value,
            Email = result.Email,
            DisplayName = result.DisplayName,
            GivenName = result.GivenName,
            FamilyName = result.FamilyName,
            AvatarUrl = result.AvatarUrl
        };
    }
}
