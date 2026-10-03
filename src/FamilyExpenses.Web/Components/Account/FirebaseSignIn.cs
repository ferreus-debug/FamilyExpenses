using FamilyExpenses.Infrastructure.Identity;
using FamilyExpenses.Web.Identity;
using Microsoft.AspNetCore.Identity;

namespace FamilyExpenses.Web.Components.Account;

internal enum FirebaseSignInResult
{
    SignedIn,
    NotInvited,
    Failed,
}

/// <summary>
/// Turns a verified Google sign-in into an app login. Known users are signed in; a new account is
/// only created for the very first user or someone holding a valid invitation (see RegistrationPolicy).
/// </summary>
internal sealed partial class FirebaseSignIn(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    RegistrationPolicy policy,
    ILogger<FirebaseSignIn> logger)
{
    public const string LoginProvider = "Firebase";

    public async Task<FirebaseSignInResult> SignInAsync(FirebaseIdentity identity, string? inviteToken)
    {
        var user = await userManager.FindByLoginAsync(LoginProvider, identity.Uid)
            ?? await LinkExistingUserAsync(identity);

        if (user is null)
        {
            if (!await policy.IsAllowedAsync(inviteToken))
            {
                return FirebaseSignInResult.NotInvited;
            }

            user = await CreateUserAsync(identity);
            if (user is null)
            {
                return FirebaseSignInResult.Failed;
            }
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        return FirebaseSignInResult.SignedIn;
    }

    // A user who already has an account with the same (Google-verified) e-mail gets Google linked to it.
    private async Task<AppUser?> LinkExistingUserAsync(FirebaseIdentity identity)
    {
        if (!identity.EmailVerified || string.IsNullOrWhiteSpace(identity.Email))
        {
            return null;
        }

        var user = await userManager.FindByEmailAsync(identity.Email);
        if (user is null)
        {
            return null;
        }

        var result = await userManager.AddLoginAsync(user, new UserLoginInfo(LoginProvider, identity.Uid, "Google"));
        return result.Succeeded ? user : null;
    }

    private async Task<AppUser?> CreateUserAsync(FirebaseIdentity identity)
    {
        if (string.IsNullOrWhiteSpace(identity.Email))
        {
            LogNoEmail(logger);
            return null;
        }

        var email = identity.Email.Trim();
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = identity.EmailVerified,
            DisplayName = string.IsNullOrWhiteSpace(identity.Name) ? email.Split('@')[0] : identity.Name.Trim(),
        };

        var created = await userManager.CreateAsync(user);
        var linked = created.Succeeded
            ? await userManager.AddLoginAsync(user, new UserLoginInfo(LoginProvider, identity.Uid, "Google"))
            : created;

        if (!linked.Succeeded)
        {
            LogCreateFailed(logger, string.Join(", ", linked.Errors.Select(e => e.Code)));
            return null;
        }

        return user;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Google sign-in without an e-mail address; cannot create an account")]
    private static partial void LogNoEmail(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not create account for Google sign-in: {Errors}")]
    private static partial void LogCreateFailed(ILogger logger, string errors);
}
