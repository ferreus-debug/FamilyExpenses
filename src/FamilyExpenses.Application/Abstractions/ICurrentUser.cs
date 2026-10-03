namespace FamilyExpenses.Application.Abstractions;

/// <summary>The logged-in user. Implemented by the web layer.</summary>
public interface ICurrentUser
{
    /// <summary>The Identity user id, or <c>null</c> when not logged in.</summary>
    string? UserId { get; }
}
