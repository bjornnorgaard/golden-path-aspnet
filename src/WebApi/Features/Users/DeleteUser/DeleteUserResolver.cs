using System.Diagnostics;
using WebApi.Telemetry;
using WebApi.Users.Contracts;
using WebApi.Users.GraphQl;
using UserId = WebApi.Database.Models.UserId;

namespace WebApi.Features.Users.DeleteUser;

internal sealed class DeleteUserResolver(DeleteUserHandler handler) : IDeleteUserResolver
{
    public async Task<DeleteUserResponse> ResolveAsync(DeleteUserRequest input, CancellationToken ct)
    {
        var userId = UserId.MustParse(input.Id);
        Activity.Current?.SetUserId(userId);

        var result = await handler.HandleAsync(new DeleteUserHandler.Command { Id = userId }, ct);
        if (result is null)
        {
            throw new HotChocolate.GraphQLException("User was not found.");
        }

        return new DeleteUserResponse
        {
            Id = result.Id.Value
        };
    }
}
