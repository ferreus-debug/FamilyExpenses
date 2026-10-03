namespace FamilyExpenses.Web.Identity;

/// <summary>
/// The Firebase project used for "Log ind med Google". All values are public client identifiers
/// (they end up in the browser anyway); access is controlled by token validation and invitations.
/// </summary>
public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public string ApiKey { get; set; } = string.Empty;

    public string AuthDomain { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Firebase ID tokens are issued by this URL, which also serves the signing keys.</summary>
    public string Issuer => $"https://securetoken.google.com/{ProjectId}";
}
