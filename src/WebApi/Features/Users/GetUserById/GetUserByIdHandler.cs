using Microsoft.EntityFrameworkCore;
using WebApi.Annotations;
using WebApi.Database;
using UserId = WebApi.Database.Models.UserId;

namespace WebApi.Features.Users.GetUserById;

[Service(ServiceLifetime.Transient)]
internal sealed class GetUserByIdHandler(TodoContext context)
{
    public class Command
    {
        public UserId Id { get; set; }
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

    public Task<Result?> HandleAsync(Command request, CancellationToken ct)
    {
        return context.Users
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
            .FirstOrDefaultAsync(ct);
    }
}
