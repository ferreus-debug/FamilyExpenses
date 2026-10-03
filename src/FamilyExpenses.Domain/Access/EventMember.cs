using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Domain.Access;

/// <summary>Links a user login to an event and (for family members) to their household.</summary>
public sealed class EventMember
{
    public EventMember(Guid eventId, string userId, Guid? householdId, MemberRole role)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainException("Medlemmet skal have en bruger.");
        }

        if (role == MemberRole.Member && householdId is null)
        {
            throw new DomainException("Et familiemedlem skal tilknyttes en husstand.");
        }

        Id = Guid.NewGuid();
        EventId = eventId;
        UserId = userId;
        HouseholdId = householdId;
        Role = role;
    }

    // For EF Core.
    private EventMember()
    {
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    /// <summary>The household the user belongs to. Optional for the admin.</summary>
    public Guid? HouseholdId { get; private set; }

    public MemberRole Role { get; private set; }

    public bool IsAdmin => Role == MemberRole.Admin;

    public void AssignHousehold(Guid householdId) => HouseholdId = householdId;
}
