namespace FamilyExpenses.Application.Settlements;

public sealed record SettlementDto(
    decimal Total,
    decimal TotalWeight,
    bool IsSettled,
    IReadOnlyList<HouseholdBalanceDto> Balances,
    IReadOnlyList<TransferDto> Transfers);

/// <param name="Balance">Positive: should receive. Negative: should pay.</param>
/// <param name="PaidByPerson">Informative breakdown of who in the household paid what.</param>
public sealed record HouseholdBalanceDto(
    Guid HouseholdId,
    string Name,
    decimal Weight,
    decimal Paid,
    decimal Share,
    decimal Balance,
    IReadOnlyList<PersonPaidDto> PaidByPerson);

public sealed record PersonPaidDto(Guid ParticipantId, string Name, decimal Paid);

public sealed record TransferDto(Guid FromHouseholdId, string FromName, Guid ToHouseholdId, string ToName, decimal Amount);
