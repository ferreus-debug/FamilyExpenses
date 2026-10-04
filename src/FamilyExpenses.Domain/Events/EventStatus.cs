namespace FamilyExpenses.Domain.Events;

/// <summary>Open → Closed (the trip is over; waiting for every household to approve) → Settled (amounts are final).</summary>
public enum EventStatus
{
    /// <summary>Expenses and households can be changed.</summary>
    Open,

    /// <summary>Locked for changes; each family (and the extra person) checks the expenses and approves.</summary>
    Closed,

    /// <summary>Everyone has approved, so who pays whom is final and payments can be registered.</summary>
    Settled,
}
