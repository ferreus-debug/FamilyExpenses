namespace FamilyExpenses.Domain.Events;

/// <summary>A household's approval of the expenses of a closed event.</summary>
public sealed class HouseholdApproval
{
    internal HouseholdApproval(Guid householdId, string approvedByUserId, DateTimeOffset approvedAt)
    {
        Id = Guid.NewGuid();
        HouseholdId = householdId;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = approvedAt;
    }

    // For EF Core.
    private HouseholdApproval()
    {
    }

    public Guid Id { get; private set; }

    public Guid HouseholdId { get; private set; }

    public string ApprovedByUserId { get; private set; } = string.Empty;

    public DateTimeOffset ApprovedAt { get; private set; }
}
