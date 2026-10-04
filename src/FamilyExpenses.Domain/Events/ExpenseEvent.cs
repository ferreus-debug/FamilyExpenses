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
    private readonly List<Payment> _payments = [];
    private readonly List<HouseholdApproval> _approvals = [];

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

    public EventStatus Status { get; private set; }

    /// <summary>Every family has approved; who pays whom is final.</summary>
    public bool IsSettled => Status == EventStatus.Settled;

    /// <summary>Closed or settled: nothing can be changed until the event is reopened.</summary>
    public bool IsLocked => Status != EventStatus.Open;

    public IReadOnlyList<Household> Households => _households;

    public IReadOnlyList<Expense> Expenses => _expenses;

    public IReadOnlyList<Payment> Payments => _payments;

    public IReadOnlyList<HouseholdApproval> Approvals => _approvals;

    /// <summary>
    /// The households that must approve a closed event: every family with people in it, and the extra person.
    /// </summary>
    public IEnumerable<Household> HouseholdsToApprove => _households.Where(h => h.Participants.Count > 0);

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
        EnsureNoPayments(family, "Familien");
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
            EnsureExpensesKeepWeight(existing.Participants[0], type);
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
        EnsureNoPayments(extra, "Den ekstra person");
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

        EnsureExpensesKeepWeight(participant, type);
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

    // ---- Pictures ---------------------------------------------------------------------------

    /// <param name="existingPictures">How many pictures the expense already has.</param>
    public ExpensePicture CreatePicture(
        Guid expenseId,
        int existingPictures,
        byte[] image,
        byte[] thumbnail,
        string createdByUserId,
        DateTimeOffset createdAt)
    {
        EnsureOpen();
        var expense = GetExpense(expenseId);
        if (existingPictures >= ExpensePicture.MaxPerExpense)
        {
            throw new DomainException($"En udgift kan højst have {ExpensePicture.MaxPerExpense} billeder.");
        }

        return new ExpensePicture(Id, expense.Id, image, thumbnail, createdByUserId, createdAt);
    }

    /// <summary>Pictures can't be removed from a settled event either.</summary>
    public void EnsurePicturesCanChange() => EnsureOpen();

    // ---- Payments ---------------------------------------------------------------------------
    // Only once the amounts are final, i.e. when every household has approved.

    public Payment RecordPayment(
        Guid fromHouseholdId,
        Guid toHouseholdId,
        Money amount,
        DateOnly date,
        string createdByUserId,
        DateTimeOffset createdAt)
    {
        if (!IsSettled)
        {
            throw new DomainException("Betalinger kan først registreres, når alle har godkendt.");
        }

        GetHousehold(fromHouseholdId);
        GetHousehold(toHouseholdId);
        var payment = new Payment(fromHouseholdId, toHouseholdId, amount, date, createdByUserId, createdAt);
        _payments.Add(payment);
        return payment;
    }

    public void RemovePayment(Guid paymentId) => _payments.Remove(GetPayment(paymentId));

    public Payment GetPayment(Guid paymentId) =>
        _payments.SingleOrDefault(p => p.Id == paymentId)
        ?? throw new DomainException("Betalingen findes ikke i begivenheden.");

    // ---- Settlement -------------------------------------------------------------------------

    public Settlement CalculateSettlement() => SettlementCalculator.Calculate(this);

    // ---- Closing & approval -----------------------------------------------------------------

    /// <summary>The trip is over: locks the event so every family can check and approve the expenses.</summary>
    public void Close()
    {
        EnsureOpen();
        if (!HouseholdsToApprove.Any())
        {
            throw new DomainException("Tilføj mindst én familie med personer, før turen lukkes.");
        }

        _approvals.Clear();
        Status = EventStatus.Closed;
    }

    /// <summary>A household approves the expenses. When the last one does, the amounts become final.</summary>
    public HouseholdApproval Approve(Guid householdId, string approvedByUserId, DateTimeOffset approvedAt)
    {
        if (Status != EventStatus.Closed)
        {
            throw new DomainException(IsSettled
                ? "Alle har allerede godkendt."
                : "Turen skal lukkes, før der kan godkendes.");
        }

        if (string.IsNullOrWhiteSpace(approvedByUserId))
        {
            throw new DomainException("Godkendelsen skal have en bruger.");
        }

        var household = GetHousehold(householdId);
        if (!HouseholdsToApprove.Contains(household))
        {
            throw new DomainException($"{household.Name} har ingen personer og skal ikke godkende.");
        }

        if (IsApproved(householdId))
        {
            throw new DomainException($"{household.Name} har allerede godkendt.");
        }

        var approval = new HouseholdApproval(householdId, approvedByUserId, approvedAt);
        _approvals.Add(approval);
        if (HouseholdsToApprove.All(h => IsApproved(h.Id)))
        {
            Status = EventStatus.Settled;
        }

        return approval;
    }

    /// <summary>Takes a household's approval back, as long as the others have not all approved yet.</summary>
    public void WithdrawApproval(Guid householdId)
    {
        if (Status != EventStatus.Closed)
        {
            throw new DomainException("En godkendelse kan kun trækkes tilbage, mens der ventes på de andre.");
        }

        var approval = _approvals.SingleOrDefault(a => a.HouseholdId == householdId)
            ?? throw new DomainException("Husstanden har ikke godkendt.");
        _approvals.Remove(approval);
    }

    public bool IsApproved(Guid householdId) => _approvals.Any(a => a.HouseholdId == householdId);

    /// <summary>Opens the event for changes again; all approvals are dropped. Registered payments stay.</summary>
    public void Reopen()
    {
        _approvals.Clear();
        Status = EventStatus.Open;
    }

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

        var ids = sharedWithParticipantIds is null
            ? [.. Participants.Select(p => p.Id)]
            : sharedWithParticipantIds.Distinct().ToList();

        // An empty list is reported by Expense itself.
        if (ids.Count > 0 && ids.Select(id => GetParticipant(id).Weight).Sum() == Weight.Zero)
        {
            throw new DomainException("Udgiften skal deles med mindst én voksen eller ét barn (babyer tæller 0).");
        }

        return ids;
    }

    /// <summary>
    /// Every expense must be shared by someone who counts, or it can't be split. Changing a person to a
    /// type with weight 0 (baby) is refused if that would leave one of their expenses with nobody who counts.
    /// </summary>
    private void EnsureExpensesKeepWeight(Participant participant, ParticipantType newType)
    {
        if (!Enum.IsDefined(newType) || Weight.For(newType) != Weight.Zero)
        {
            return;
        }

        var participants = Participants.ToDictionary(p => p.Id);
        var orphaned = _expenses.Any(e =>
            e.SharedWithParticipantIds.Contains(participant.Id)
            && e.SharedWithParticipantIds.Where(id => id != participant.Id).All(id => participants[id].Weight == Weight.Zero));
        if (orphaned)
        {
            throw new DomainException(
                $"{participant.Name} kan ikke ændres til baby, fordi en udgift så ikke deles med nogen der tæller med.");
        }
    }

    private int NextHouseholdSortOrder() =>
        _households.Count == 0 ? 0 : _households.Max(h => h.SortOrder) + 1;

    private void EnsureOpen()
    {
        if (IsLocked)
        {
            throw new DomainException(IsSettled
                ? "Begivenheden er afregnet og kan ikke ændres. Genåbn den først."
                : "Turen er lukket og venter på godkendelse. Genåbn den for at ændre noget.");
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

    private void EnsureNoPayments(Household household, string who)
    {
        if (_payments.Any(p => p.References(household.Id)))
        {
            throw new DomainException($"{who} har registrerede betalinger og kan ikke fjernes.");
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
