using Microsoft.AspNetCore.Http.HttpResults;
using WebApi.Users.Contracts;
using WebApi.Users.Endpoints;

namespace WebApi.Features.Users.GetUserList;

internal sealed class GetUserListEndpoint(GetUserListHandler handler) : IGetUserListEndpoint
{
    public async Task<Results<Ok<GetUserListResponse>, BadRequest<IReadOnlyDictionary<string, string[]>>>> HandleAsync(
        GetUserListRequest request,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetUserListHandler.Command
        {
            Limit = request.Limit,
            Offset = request.Offset
        }, ct);

        return TypedResults.Ok(new GetUserListResponse
        {
            Users = result.Users
        });
    }
}
