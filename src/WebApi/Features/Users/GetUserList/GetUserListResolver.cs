using HotChocolate.Execution.Processing;
using HotChocolate.Resolvers;
using Microsoft.EntityFrameworkCore;
using WebApi.Configuration;
using WebApi.Database;
using WebApi.Users.Contracts;
using WebApi.Users.GraphQl;

namespace WebApi.Features.Users.GetUserList;

internal sealed class GetUserListResolver(TodoContext context, PagingOptions paging) : IGetUserListResolver
{
    public async Task<GetUserListResponse> ResolveAsync(GetUserListRequest input, IResolverContext resolverContext, CancellationToken ct)
    {
        var effectiveLimit = Math.Min(input.Limit ?? paging.DefaultPageSize, paging.MaxPageSize);
        var effectiveOffset = input.Offset switch
        {
            null or < 0 => 0,
            { } offset => offset
        };

        var usersSelections = resolverContext.Select("users");
        if (usersSelections.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one selection for the 'users' field but found {usersSelections.Count}.");
        }

        var usersSelection = (Selection)usersSelections[0];

        var users = await context.Users
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .Skip(effectiveOffset)
            .Take(effectiveLimit)
            .Select(user => new GetUserListItem
            {
                Id = user.Id.Value,
                Email = user.Email,
                DisplayName = user.DisplayName,
                GivenName = user.GivenName,
                FamilyName = user.FamilyName,
                AvatarUrl = user.AvatarUrl
            })
            .Select(usersSelection)
            .ToArrayAsync(ct);

        return new GetUserListResponse
        {
            Users = users
        };
    }
}
