using Microsoft.EntityFrameworkCore;
using WebApi.Annotations;
using WebApi.Database;
using UserId = WebApi.Database.Models.UserId;

namespace WebApi.Features.Users.UpdateUser;

[Service(ServiceLifetime.Transient)]
internal sealed class UpdateUserHandler(TodoContext context)
{
    public class Command
    {
        public UserId Id { get; set; }
        public string? DisplayName { get; set; }
        public string? GivenName { get; set; }
        public string? FamilyName { get; set; }
        public string? AvatarUrl { get; set; }
    }

    public class Result
    {
        public UserId Id { get; set; }
        public string Email { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? GivenName { get; set; }
        public string? FamilyName { get; set; }
        public string? AvatarUrl { get; set; }
    }

    public async Task<Result?> HandleAsync(Command request, CancellationToken ct)
    {
        var updated = await context.Users
            .Where(user => user.Id == request.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.DisplayName, request.DisplayName)
                .SetProperty(user => user.GivenName, request.GivenName)
                .SetProperty(user => user.FamilyName, request.FamilyName)
                .SetProperty(user => user.AvatarUrl, request.AvatarUrl), ct);

        if (updated <= 0)
        {
            return null;
        }

        // Email isn't part of the update, so it isn't known from the command - a lightweight
        // no-tracking read is cheaper than loading and mutating a tracked entity before saving.
        return await context.Users
            .AsNoTracking()
            .Where(user => user.Id == request.Id)
            .Select(user => new Result
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                GivenName = user.GivenName,
                FamilyName = user.FamilyName,
                AvatarUrl = user.AvatarUrl
            })
            .FirstAsync(ct);
    }
}
