namespace FamilyExpenses.Application.Invitations;

/// <param name="Token">The raw token for the link. Only available right after creation.</param>
public sealed record CreatedInvitation(Guid Id, string Token, DateTimeOffset ExpiresAt);

public enum InvitationStatus
{
    Pending,
    Accepted,
    Expired,
}

public sealed record InvitationDto(
    Guid Id,
    Guid HouseholdId,
    string HouseholdName,
    DateTimeOffset ExpiresAt,
    InvitationStatus Status,
    string? AcceptedByName);

/// <summary>What someone opening an invitation link sees before logging in.</summary>
public sealed record InvitationPreview(string EventName, string HouseholdName, bool IsValid);
