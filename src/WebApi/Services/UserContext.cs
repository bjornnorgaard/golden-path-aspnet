using System.Security.Claims;
using WebApi.Annotations;
using WebApi.Database.Models;

namespace WebApi.Services;

public interface IUserContext
{
    UserId? CurrentUserId { get; }
}

[Service(ServiceLifetime.Scoped, As = typeof(IUserContext))]
public class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public UserId? CurrentUserId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var guid) ? new UserId(guid) : null;
        }
    }
}
