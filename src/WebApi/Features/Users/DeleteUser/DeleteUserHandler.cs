using Microsoft.EntityFrameworkCore;
using WebApi.Annotations;
using WebApi.Database;
using WebApi.Database.Models;

namespace WebApi.Features.Users.DeleteUser;

[Service(ServiceLifetime.Transient)]
internal sealed class DeleteUserHandler(TodoContext context)
{
    public class Command
    {
        public UserId Id { get; set; }
    }

    public class Result
    {
        public UserId Id { get; set; }
    }

    public async Task<Result?> HandleAsync(Command request, CancellationToken ct)
    {
        var deleted = await context.Users
            .Where(user => user.Id == request.Id)
            .ExecuteDeleteAsync(ct);

        if (deleted <= 0)
        {
            return null;
        }

        return new Result { Id = request.Id };
    }
}
