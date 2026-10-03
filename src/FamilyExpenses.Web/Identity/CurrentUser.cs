using System.Security.Claims;
using FamilyExpenses.Application.Abstractions;

namespace FamilyExpenses.Web.Identity;

/// <summary>
/// The user of the current circuit (interactive) or request (static rendering).
/// Kept up to date by <see cref="UserCircuitHandler"/>.
/// </summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public ClaimsPrincipal? Principal { get; set; }

    public string? UserId
    {
        get
        {
            var principal = Principal ?? httpContextAccessor.HttpContext?.User;
            return principal?.Identity?.IsAuthenticated == true
                ? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                : null;
        }
    }
}
