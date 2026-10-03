using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Domain.Events;

public sealed class Expense
{
    public const int MaxDescriptionLength = 200;

    private List<Guid> _sharedWithParticipantIds = [];

    internal Expense(
        string description,
        Money amount,
        DateOnly date,
        Guid paidByParticipantId,
        IReadOnlyCollection<Guid> sharedWithParticipantIds,
        string createdByUserId)
    {
        Id = Guid.NewGuid();
        CreatedByUserId = createdByUserId;
        Update(description, amount, date, paidByParticipantId, sharedWithParticipantIds);
    }

    public Guid Id { get; }

    public string Description { get; private set; } = string.Empty;

    public Money Amount { get; private set; }

    public DateOnly Date { get; private set; }

    /// <summary>The person who paid. The payer's household is credited in the settlement.</summary>
    public Guid PaidByParticipantId { get; private set; }

    /// <summary>
    /// The people the expense is split between. Always explicit: "everyone" is resolved when the expense
    /// is registered, so people added later do not change earlier expenses.
    /// </summary>
    public IReadOnlyList<Guid> SharedWithParticipantIds => _sharedWithParticipantIds;

    public string CreatedByUserId { get; }

    internal bool References(Guid participantId) =>
        PaidByParticipantId == participantId || _sharedWithParticipantIds.Contains(participantId);

    internal void Update(
        string description,
        Money amount,
        DateOnly date,
        Guid paidByParticipantId,
        IReadOnlyCollection<Guid> sharedWithParticipantIds)
    {
        var trimmed = description?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException("Beskrivelse skal udfyldes.");
        }

        if (trimmed.Length > MaxDescriptionLength)
        {
            throw new DomainException($"Beskrivelse må højst være {MaxDescriptionLength} tegn.");
        }

        if (amount <= Money.Zero)
        {
            throw new DomainException("Beløbet skal være større end 0.");
        }

        if (sharedWithParticipantIds.Count == 0)
        {
            throw new DomainException("Udgiften skal deles mellem mindst én deltager.");
        }

        Description = trimmed;
        Amount = amount;
        Date = date;
        PaidByParticipantId = paidByParticipantId;
        _sharedWithParticipantIds = [.. sharedWithParticipantIds.Distinct()];
    }
}
