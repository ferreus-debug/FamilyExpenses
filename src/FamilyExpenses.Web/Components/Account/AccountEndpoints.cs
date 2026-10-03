using FamilyExpenses.Infrastructure.Identity;
using FamilyExpenses.Web.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FamilyExpenses.Web.Components.Account;

internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Form post from GoogleSignInForm (antiforgery-protected) carrying the Firebase ID token.
        endpoints.MapPost("/Account/FirebaseLogin", async (
            [FromForm] string? idToken,
            [FromForm] string? returnUrl,
            [FromForm] string? invite,
            IFirebaseTokenValidator validator,
            FirebaseSignIn signIn,
            CancellationToken cancellationToken) =>
        {
            var identity = await validator.ValidateAsync(idToken ?? string.Empty, cancellationToken);
            if (identity is null)
            {
                return LoginError("failed", returnUrl);
            }

            var inviteToken = string.IsNullOrWhiteSpace(invite) ? null : invite;
            return await signIn.SignInAsync(identity, inviteToken) switch
            {
                FirebaseSignInResult.SignedIn => TypedResults.LocalRedirect(inviteToken is null
                    ? $"~{ReturnUrls.Safe(returnUrl)}"
                    : $"~/invite/{Uri.EscapeDataString(inviteToken)}"),
                FirebaseSignInResult.NotInvited => LoginError("not-invited", returnUrl),
                _ => LoginError("failed", returnUrl),
            };
        });

        // POST /Account/Logout (form post with antiforgery token from the app bar).
        endpoints.MapPost("/Account/Logout", async (SignInManager<AppUser> signInManager, [FromForm] string? returnUrl) =>
        {
            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "~/" : $"~/{returnUrl.TrimStart('/')}");
        });

        return endpoints;
    }

    private static RedirectHttpResult LoginError(string error, string? returnUrl) =>
        TypedResults.LocalRedirect($"~/Account/Login?error={error}&ReturnUrl={Uri.EscapeDataString(ReturnUrls.Safe(returnUrl))}");
}
