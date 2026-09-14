using System.Diagnostics;
using HotChocolate.Resolvers;
using Microsoft.EntityFrameworkCore;
using WebApi.Database;
using WebApi.Telemetry;
using WebApi.Users.Contracts;
using WebApi.Users.GraphQl;
using UserId = WebApi.Database.Models.UserId;

namespace WebApi.Features.Users.GetUserById;

internal sealed class GetUserByIdResolver(TodoContext context) : IGetUserByIdResolver
{
    public async Task<GetUserByIdResponse> ResolveAsync(GetUserByIdRequest input, IResolverContext resolverContext, CancellationToken ct)
    {
        var id = UserId.MustParse(input.Id);
        Activity.Current?.SetUserId(id);

        var result = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => new GetUserByIdResponse
            {
                Id = user.Id.Value,
                Email = user.Email,
                DisplayName = user.DisplayName,
                GivenName = user.GivenName,
                FamilyName = user.FamilyName,
                AvatarUrl = user.AvatarUrl
            })
            .Select(resolverContext.Selection)
            .FirstOrDefaultAsync(ct);

        if (result is null)
        {
            throw new HotChocolate.GraphQLException("User was not found.");
        }

        return result;
    }
}
