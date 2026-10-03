using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Settlements;

namespace FamilyExpenses.Domain.Events;

/// <summary>
/// Aggregate root: an occasion (e.g. a holiday) with its households, participants and expenses.
/// All changes go through this class so the business rules are enforced in one place.
/// </summary>
public sealed class ExpenseEvent
{
    /// <summary>Enough for a trip with friends (a friend travelling alone is a household of one adult).</summary>
    public const int MaxFamilies = 12;

    private readonly List<Household> _households = [];
    private readonly List<Expense> _expenses = [];

    public ExpenseEvent(string name, string currency = "DKK")
    {
        Id = Guid.NewGuid();
        Rename(name);
        Currency = Guard.Name(currency, "Valuta");
    }

    // For EF Core.
    private ExpenseEvent()
    {
        Currency = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Currency { get; private set; }

    /// <summary>When settled, the event is locked for changes until it is reopened.</summary>
    public bool IsSettled { get; private set; }

    public IReadOnlyList<Household> Households => _households;

    public IReadOnlyList<Expense> Expenses => _expenses;

    public IEnumerable<Household> Families => _households.Where(h => h.Kind == HouseholdKind.Family);

    public Household? ExtraPerson => _households.SingleOrDefault(h => h.Kind == HouseholdKind.ExtraPerson);

    public IEnumerable<Participant> Participants => _households.SelectMany(h => h.Participants);

    public void Rename(string name)
    {
        EnsureOpen();
        Name = Guard.Name(name, "Navn på begivenhed");
    }

    // ---- Households -------------------------------------------------------------------------

    public Household AddFamily(string name)
    {
        EnsureOpen();
        if (Families.Count() >= MaxFamilies)
        {
            throw new DomainException($"Der kan højst være {MaxFamilies} familier.");
        }

        var family = new Household(name, HouseholdKind.Family, NextHouseholdSortOrder());
        EnsureUniqueHouseholdName(family.Name, exceptId: null);
        _households.Add(family);
        return family;
    }

    public void RenameHousehold(Guid householdId, string name)
    {
        EnsureOpen();
        var household = GetHousehold(householdId);
        EnsureUniqueHouseholdName(name, exceptId: householdId);
        household.Rename(name);
    }

    public void RemoveFamily(Guid householdId)
    {
        EnsureOpen();
        var family = GetHousehold(householdId);
        if (family.Kind != HouseholdKind.Family)
        {
            throw new DomainException("Brug 'fjern ekstra person' til at fjerne den ekstra person.");
        }

        EnsureNotReferenced(family.Participants, "Familien");
        _households.Remove(family);
    }

    /// <summary>
    /// Adds the extra person, or updates name/type if one already exists (the id is kept, so
    /// existing expenses stay valid and are simply re-weighted).
    /// </summary>
    public Household SetExtraPerson(string name, ParticipantType type)
    {
        EnsureOpen();
        var existing = ExtraPerson;
        if (existing is not null)
        {
            EnsureUniqueHouseholdName(name, exceptId: existing.Id);
            existing.Participants[0].Update(name, type);
            existing.Rename(name);
            return existing;
        }

        var household = new Household(name, HouseholdKind.ExtraPerson, NextHouseholdSortOrder());
        EnsureUniqueHouseholdName(household.Name, exceptId: null);
        household.AddParticipant(name, type);
        _households.Add(household);
        return household;
    }

    public void RemoveExtraPerson()
    {
        EnsureOpen();
        var extra = ExtraPerson ?? throw new DomainException("Der er ingen ekstra person.");
        EnsureNotReferenced(extra.Participants, "Den ekstra person");
        _households.Remove(extra);
    }

    // ---- Participants -----------------------------------------------------------------------

    public Participant AddParticipant(Guid familyId, string name, ParticipantType type)
    {
        EnsureOpen();
        var family = GetHousehold(familyId);
        if (family.Kind != HouseholdKind.Family)
        {
            throw new DomainException("Deltagere kan kun tilføjes til en familie.");
        }

        return family.AddParticipant(name, type);
    }

    public void UpdateParticipant(Guid participantId, string name, ParticipantType type)
    {
        EnsureOpen();
        var participant = GetParticipant(participantId);
        var household = GetHousehold(participant.HouseholdId);
        if (household.Kind == HouseholdKind.ExtraPerson)
        {
            SetExtraPerson(name, type);
            return;
        }

        participant.Update(name, type);
    }

    public void RemoveParticipant(Guid participantId)
    {
        EnsureOpen();
        var participant = GetParticipant(participantId);
        var household = GetHousehold(participant.HouseholdId);
        if (household.Kind == HouseholdKind.ExtraPerson)
        {
            throw new DomainException("Brug 'fjern ekstra person' til at fjerne den ekstra person.");
        }

        EnsureNotReferenced([participant], participant.Name);
        household.RemoveParticipant(participant);
    }

    // ---- Expenses ---------------------------------------------------------------------------

    /// <param name="sharedWithParticipantIds">Who shares the expense; <c>null</c> means everyone right now.</param>
    public Expense AddExpense(
        string description,
        Money amount,
        DateOnly date,
        Guid paidByParticipantId,
        IEnumerable<Guid>? sharedWithParticipantIds,
        string createdByUserId)
    {
        EnsureOpen();
        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new DomainException("Udgiften skal have en opretter.");
        }

        var sharedWith = ResolveSharedWith(paidByParticipantId, sharedWithParticipantIds);
        var expense = new Expense(description, amount, date, paidByParticipantId, sharedWith, createdByUserId);
        _expenses.Add(expense);
        return expense;
    }

    public void UpdateExpense(
        Guid expenseId,
        string description,
        Money amount,
        DateOnly date,
        Guid paidByParticipantId,
        IEnumerable<Guid>? sharedWithParticipantIds)
    {
        EnsureOpen();
        var expense = GetExpense(expenseId);
        var sharedWith = ResolveSharedWith(paidByParticipantId, sharedWithParticipantIds);
        expense.Update(description, amount, date, paidByParticipantId, sharedWith);
    }

    public void RemoveExpense(Guid expenseId)
    {
        EnsureOpen();
        _expenses.Remove(GetExpense(expenseId));
    }

    // ---- Settlement -------------------------------------------------------------------------

    public Settlement CalculateSettlement() => SettlementCalculator.Calculate(this);

    public void MarkSettled()
    {
        EnsureOpen();
        IsSettled = true;
    }

    public void Reopen() => IsSettled = false;

    // ---- Lookups & rules --------------------------------------------------------------------

    public Household GetHousehold(Guid householdId) =>
        _households.SingleOrDefault(h => h.Id == householdId)
        ?? throw new DomainException("Husstanden findes ikke i begivenheden.");

    public Participant GetParticipant(Guid participantId) =>
        Participants.SingleOrDefault(p => p.Id == participantId)
        ?? throw new DomainException("Deltageren findes ikke i begivenheden.");

    public Expense GetExpense(Guid expenseId) =>
        _expenses.SingleOrDefault(e => e.Id == expenseId)
        ?? throw new DomainException("Udgiften findes ikke i begivenheden.");

    private List<Guid> ResolveSharedWith(Guid paidByParticipantId, IEnumerable<Guid>? sharedWithParticipantIds)
    {
        GetParticipant(paidByParticipantId);

        if (sharedWithParticipantIds is null)
        {
            return [.. Participants.Select(p => p.Id)];
        }

        var ids = sharedWithParticipantIds.Distinct().ToList();
        foreach (var id in ids)
        {
            GetParticipant(id);
        }

        return ids;
    }

    private int NextHouseholdSortOrder() =>
        _households.Count == 0 ? 0 : _households.Max(h => h.SortOrder) + 1;

    private void EnsureOpen()
    {
        if (IsSettled)
        {
            throw new DomainException("Begivenheden er afregnet og kan ikke ændres. Genåbn den først.");
        }
    }

    private void EnsureUniqueHouseholdName(string name, Guid? exceptId)
    {
        name = Guard.Name(name, "Navn på husstand");
        if (_households.Any(h => h.Id != exceptId && string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"Der findes allerede en husstand med navnet '{name}'.");
        }
    }

    private void EnsureNotReferenced(IEnumerable<Participant> participants, string who)
    {
        if (participants.Any(p => _expenses.Any(e => e.References(p.Id))))
        {
            throw new DomainException($"{who} indgår i en eller flere udgifter og kan ikke fjernes.");
        }
    }
}
