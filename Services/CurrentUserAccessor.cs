using System.Security.Claims;

namespace Ventagram.Services;

public class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
{
    public int? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
