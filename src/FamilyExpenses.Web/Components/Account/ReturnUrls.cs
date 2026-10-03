namespace FamilyExpenses.Web.Components.Account;

internal static class ReturnUrls
{
    /// <summary>Only allow app-relative return URLs, so a login link can't redirect to another site.</summary>
    public static string Safe(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || !Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
            || returnUrl.StartsWith("//", StringComparison.Ordinal)
            || returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl.StartsWith('/') ? returnUrl : "/" + returnUrl;
    }
}
