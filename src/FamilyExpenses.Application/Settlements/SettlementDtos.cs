namespace FamilyExpenses.Application.Settlements;

/// <param name="IsSettled">Every family has approved; until then the numbers are preliminary.</param>
/// <param name="Transfers">What is still to be paid, after the payments already registered.</param>
/// <param name="Payments">Payments already registered, newest first.</param>
public sealed record SettlementDto(
    decimal Total,
    decimal TotalWeight,
    bool IsSettled,
    IReadOnlyList<HouseholdBalanceDto> Balances,
    IReadOnlyList<TransferDto> Transfers,
    IReadOnlyList<PaymentDto> Payments);

/// <param name="Transferred">Paid to other households minus received from them.</param>
/// <param name="Balance">Positive: should receive. Negative: should pay.</param>
/// <param name="PaidByPerson">Informative breakdown of who in the household paid what.</param>
/// <param name="Expenses">The expenses the household paid or took part in, oldest first.</param>
public sealed record HouseholdBalanceDto(
    Guid HouseholdId,
    string Name,
    decimal Weight,
    decimal Paid,
    decimal Share,
    decimal Transferred,
    decimal Balance,
    IReadOnlyList<PersonPaidDto> PaidByPerson,
    IReadOnlyList<ExpenseShareDto> Expenses);

public sealed record PersonPaidDto(Guid ParticipantId, string Name, decimal Paid);

/// <param name="HouseholdWeight">The weight of the household's people sharing the expense; null if none of them did.</param>
/// <param name="SharedWeight">The weight of everyone sharing the expense.</param>
public sealed record ExpenseShareDto(
    Guid ExpenseId,
    string Description,
    DateOnly Date,
    decimal Paid,
    decimal Share,
    decimal? HouseholdWeight,
    decimal SharedWeight);

/// <param name="CanMarkPaid">The current user may register this transfer as paid.</param>
public sealed record TransferDto(
    Guid FromHouseholdId,
    string FromName,
    Guid ToHouseholdId,
    string ToName,
    decimal Amount,
    bool CanMarkPaid);

public sealed record PaymentDto(
    Guid Id,
    Guid FromHouseholdId,
    string FromName,
    Guid ToHouseholdId,
    string ToName,
    decimal Amount,
    DateOnly Date,
    string CreatedByName,
    bool CanRemove);
