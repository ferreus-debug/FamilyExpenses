using FamilyExpenses.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FamilyExpenses.Web.Components.Account;

internal static class AccountEndpoints
{
    /// <summary>POST /Account/Logout (form post with antiforgery token from the app bar).</summary>
    public static IEndpointConventionBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/Account/Logout", async (SignInManager<AppUser> signInManager, [FromForm] string? returnUrl) =>
        {
            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "~/" : $"~/{returnUrl.TrimStart('/')}");
        });
}
