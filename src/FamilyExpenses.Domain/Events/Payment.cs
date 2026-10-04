using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Domain.Events;

/// <summary>
/// Money one household has actually transferred to another to settle up. Payments count in the settlement,
/// so the suggested transfers shrink as they are paid, and stay correct if expenses change afterwards.
/// </summary>
public sealed class Payment
{
    internal Payment(
        Guid fromHouseholdId,
        Guid toHouseholdId,
        Money amount,
        DateOnly date,
        string createdByUserId,
        DateTimeOffset createdAt)
    {
        if (fromHouseholdId == toHouseholdId)
        {
            throw new DomainException("En husstand kan ikke betale til sig selv.");
        }

        if (amount <= Money.Zero)
        {
            throw new DomainException("Beløbet skal være større end 0.");
        }

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new DomainException("Betalingen skal have en opretter.");
        }

        Id = Guid.NewGuid();
        FromHouseholdId = fromHouseholdId;
        ToHouseholdId = toHouseholdId;
        Amount = amount;
        Date = date;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    // For EF Core.
    private Payment()
    {
    }

    public Guid Id { get; private set; }

    public Guid FromHouseholdId { get; private set; }

    public Guid ToHouseholdId { get; private set; }

    public Money Amount { get; private set; }

    public DateOnly Date { get; private set; }

    public string CreatedByUserId { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    internal bool References(Guid householdId) => FromHouseholdId == householdId || ToHouseholdId == householdId;
}
