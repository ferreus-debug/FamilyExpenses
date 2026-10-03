using Microsoft.AspNetCore.Identity;

namespace FamilyExpenses.Infrastructure.Identity;

public sealed class AppUser : IdentityUser
{
    /// <summary>Name shown in the app, e.g. "Anna".</summary>
    public string DisplayName { get; set; } = string.Empty;
}
