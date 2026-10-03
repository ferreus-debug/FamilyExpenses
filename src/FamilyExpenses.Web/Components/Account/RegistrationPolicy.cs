using FamilyExpenses.Application.Invitations;
using FamilyExpenses.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FamilyExpenses.Web.Components.Account;

/// <summary>
/// Who may create an account: the very first user (the owner setting up the Pi), or anyone
/// holding a valid invitation link. This keeps strangers off a publicly reachable app.
/// </summary>
internal sealed class RegistrationPolicy(UserManager<AppUser> userManager, InvitationService invitations)
{
    public async Task<bool> IsAllowedAsync(string? inviteToken)
    {
        if (!await userManager.Users.AnyAsync())
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(inviteToken))
        {
            return false;
        }

        var preview = await invitations.PreviewAsync(inviteToken);
        return preview?.IsValid == true;
    }
}
