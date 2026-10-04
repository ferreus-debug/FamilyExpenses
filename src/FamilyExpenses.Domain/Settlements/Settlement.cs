using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Domain.Settlements;

/// <summary>The result of settling an event: balances per household and the transfers that even them out.</summary>
public sealed record Settlement(IReadOnlyList<HouseholdBalance> Balances, IReadOnlyList<Transfer> Transfers)
{
    public Money Total => Balances.Aggregate(Money.Zero, (sum, b) => sum + b.Paid);
}

/// <param name="Weight">The household's total weight (all its participants), for display.</param>
/// <param name="Paid">What people in the household paid.</param>
/// <param name="Share">The household's share of the expenses it took part in.</param>
/// <param name="Transferred">Payments made to other households minus payments received from them.</param>
/// <param name="Expenses">How <paramref name="Paid"/> and <paramref name="Share"/> add up, expense by expense.</param>
public sealed record HouseholdBalance(
    Guid HouseholdId,
    string Name,
    Weight Weight,
    Money Paid,
    Money Share,
    Money Transferred,
    IReadOnlyList<ExpenseShare> Expenses)
{
    /// <summary>Positive: the household should receive money. Negative: it should pay.</summary>
    public Money Balance => Paid - Share + Transferred;
}

/// <summary>One expense seen from one household: what it paid and its part of the cost.</summary>
/// <param name="HouseholdWeight">The weight of the household's people sharing the expense; null if none of them did.</param>
/// <param name="SharedWeight">The weight of everyone sharing the expense.</param>
public sealed record ExpenseShare(
    Guid ExpenseId,
    string Description,
    DateOnly Date,
    Money Paid,
    Money Share,
    Weight? HouseholdWeight,
    Weight SharedWeight);

public sealed record Transfer(Guid FromHouseholdId, string FromName, Guid ToHouseholdId, string ToName, Money Amount);
