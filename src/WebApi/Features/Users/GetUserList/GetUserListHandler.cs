using Microsoft.EntityFrameworkCore;
using WebApi.Annotations;
using WebApi.Configuration;
using WebApi.Database;
using WebApi.Users.Contracts;

namespace WebApi.Features.Users.GetUserList;

[Service(ServiceLifetime.Transient)]
internal sealed class GetUserListHandler(TodoContext context, PagingOptions paging)
{
    public class Command
    {
        public int? Limit { get; set; }
        public int? Offset { get; set; }
    }

    public class Result
    {
        public GetUserListItem[] Users { get; set; } = [];
    }

    public async Task<Result> HandleAsync(Command request, CancellationToken ct)
    {
        var effectiveLimit = Math.Min(request.Limit ?? paging.DefaultPageSize, paging.MaxPageSize);
        var effectiveOffset = request.Offset switch
        {
            null or < 0 => 0,
            { } offset => offset
        };

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
            .ToArrayAsync(ct);

        return new Result { Users = users };
    }
}
