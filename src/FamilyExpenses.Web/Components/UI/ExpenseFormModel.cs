using FamilyExpenses.Application.Events;
using FamilyExpenses.Application.Expenses;

namespace FamilyExpenses.Web.Components.UI;

/// <summary>
/// State and rules of the expense form, kept out of the Razor markup so it can be unit tested.
/// "Shared with" is picked per person, with whole-household shortcuts; everyone is selected by default.
/// </summary>
public sealed class ExpenseFormModel
{
    private readonly HashSet<Guid> _selected;

    public ExpenseFormModel(IReadOnlyList<HouseholdDto> households, DateOnly today, Guid? defaultPayerId)
    {
        Households = households;
        Date = today.ToDateTime(TimeOnly.MinValue);
        PaidByParticipantId = defaultPayerId;
        _selected = [.. AllParticipants.Select(p => p.Id)];
    }

    public IReadOnlyList<HouseholdDto> Households { get; }

    public string Description { get; set; } = string.Empty;

    public decimal? Amount { get; set; }

    /// <summary>DateTime because MudDatePicker binds to DateTime?.</summary>
    public DateTime? Date { get; set; }

    public Guid? PaidByParticipantId { get; set; }

    public IEnumerable<ParticipantDto> AllParticipants => Households.SelectMany(h => h.Participants);

    public bool AllSelected => AllParticipants.All(p => _selected.Contains(p.Id));

    public int SelectedCount => _selected.Count;

    /// <summary>Total weight of the selected people, e.g. 7,5.</summary>
    public decimal SelectedWeight => AllParticipants.Where(p => _selected.Contains(p.Id)).Sum(p => p.Weight);

    public static ExpenseFormModel ForEdit(IReadOnlyList<HouseholdDto> households, ExpenseDto expense)
    {
        ArgumentNullException.ThrowIfNull(expense);
        var model = new ExpenseFormModel(households, expense.Date, expense.PaidByParticipantId)
        {
            Description = expense.Description,
            Amount = expense.Amount,
        };
        model._selected.Clear();
        model._selected.UnionWith(expense.SharedWithParticipantIds);
        return model;
    }

    public bool IsSelected(Guid participantId) => _selected.Contains(participantId);

    /// <summary>true = everyone in the household, false = nobody, null = some (indeterminate checkbox).</summary>
    public bool? HouseholdState(HouseholdDto household)
    {
        ArgumentNullException.ThrowIfNull(household);
        var count = household.Participants.Count(p => _selected.Contains(p.Id));
        return count == 0 ? false : count == household.Participants.Count ? true : null;
    }

    public void SetParticipant(Guid participantId, bool selected)
    {
        if (selected)
        {
            _selected.Add(participantId);
        }
        else
        {
            _selected.Remove(participantId);
        }
    }

    /// <summary>Clicking a household checkbox selects all of it, unless it is already fully selected.</summary>
    public void ToggleHousehold(HouseholdDto household)
    {
        var select = HouseholdState(household) != true;
        foreach (var participant in household.Participants)
        {
            SetParticipant(participant.Id, select);
        }
    }

    public void SelectAll(bool selected)
    {
        foreach (var participant in AllParticipants)
        {
            SetParticipant(participant.Id, selected);
        }
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Description))
        {
            errors.Add("Skriv hvad udgiften var til.");
        }

        if (Amount is not > 0)
        {
            errors.Add("Beløbet skal være større end 0.");
        }
        else if (decimal.Round(Amount.Value, 2) != Amount.Value)
        {
            errors.Add("Beløb må højst have to decimaler.");
        }

        if (Date is null)
        {
            errors.Add("Vælg en dato.");
        }

        if (PaidByParticipantId is null)
        {
            errors.Add("Vælg hvem der betalte.");
        }

        if (_selected.Count == 0)
        {
            errors.Add("Vælg mindst én at dele udgiften med.");
        }

        return errors;
    }

    /// <summary>Builds the service input. Call only when <see cref="Validate"/> returned no errors.</summary>
    public ExpenseInput ToInput() => new(
        Description.Trim(),
        Amount!.Value,
        DateOnly.FromDateTime(Date!.Value),
        PaidByParticipantId!.Value,
        [.. AllParticipants.Where(p => _selected.Contains(p.Id)).Select(p => p.Id)]);
}
