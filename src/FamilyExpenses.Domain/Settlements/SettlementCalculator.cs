using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Domain.Settlements;

/// <summary>
/// Pure settlement logic. Each expense is split between the households of the people sharing it,
/// in proportion to their weight (adult 1, child 0.5). All arithmetic is done in øre so totals
/// always add up exactly.
/// </summary>
public static class SettlementCalculator
{
    public static Settlement Calculate(ExpenseEvent expenseEvent)
    {
        ArgumentNullException.ThrowIfNull(expenseEvent);

        // Tie-breaks use SortOrder (not list position) so the result is identical however the data was loaded.
        var households = expenseEvent.Households.OrderBy(h => h.SortOrder).ToList();
        var order = households.ToDictionary(h => h.Id, h => h.SortOrder);
        var participants = expenseEvent.Participants.ToDictionary(p => p.Id);

        var paid = households.ToDictionary(h => h.Id, _ => 0L);
        var share = households.ToDictionary(h => h.Id, _ => 0L);

        foreach (var expense in expenseEvent.Expenses)
        {
            var amount = expense.Amount.MinorUnits;
            paid[participants[expense.PaidByParticipantId].HouseholdId] += amount;

            var weights = expense.SharedWithParticipantIds
                .Select(id => participants[id])
                .GroupBy(p => p.HouseholdId)
                .Select(g => (HouseholdId: g.Key, Weight: g.Sum(p => p.Weight.Value)))
                .ToList();

            foreach (var (householdId, minorUnits) in Allocate(amount, weights, order))
            {
                share[householdId] += minorUnits;
            }
        }

        var balances = households
            .Select(h => new HouseholdBalance(
                h.Id,
                h.Name,
                h.TotalWeight,
                Money.FromMinorUnits(paid[h.Id]),
                Money.FromMinorUnits(share[h.Id])))
            .ToList();

        return new Settlement(balances, MinimizeTransfers(balances, order));
    }

    /// <summary>
    /// Splits <paramref name="amount"/> proportionally to the weights. Each share is rounded down to whole
    /// øre and the leftover øre go to the largest remainders (ties: household order), so the sum is exact
    /// and the result is deterministic.
    /// </summary>
    internal static IEnumerable<(Guid HouseholdId, long MinorUnits)> Allocate(
        long amount,
        IReadOnlyList<(Guid HouseholdId, decimal Weight)> weights,
        IReadOnlyDictionary<Guid, int> order)
    {
        var totalWeight = weights.Sum(w => w.Weight);
        if (totalWeight <= 0m)
        {
            throw new DomainException("En udgift skal deles mellem mindst én deltager.");
        }

        var parts = weights
            .Select(w =>
            {
                var exact = amount * w.Weight / totalWeight;
                var floor = (long)decimal.Floor(exact);
                return (w.HouseholdId, MinorUnits: floor, Remainder: exact - floor);
            })
            .ToList();

        var leftover = amount - parts.Sum(p => p.MinorUnits);
        var receivers = parts
            .OrderByDescending(p => p.Remainder)
            .ThenBy(p => order[p.HouseholdId])
            .Take((int)leftover)
            .Select(p => p.HouseholdId)
            .ToHashSet();

        return parts.Select(p => (p.HouseholdId, p.MinorUnits + (receivers.Contains(p.HouseholdId) ? 1 : 0)));
    }

    /// <summary>
    /// Greedy matching of the largest debtor with the largest creditor. Produces at most
    /// (households - 1) transfers.
    /// </summary>
    private static List<Transfer> MinimizeTransfers(
        IReadOnlyList<HouseholdBalance> balances,
        IReadOnlyDictionary<Guid, int> order)
    {
        var names = balances.ToDictionary(b => b.HouseholdId, b => b.Name);
        var debtors = balances
            .Where(b => b.Balance < Money.Zero)
            .Select(b => (b.HouseholdId, Amount: -b.Balance.MinorUnits))
            .ToList();
        var creditors = balances
            .Where(b => b.Balance > Money.Zero)
            .Select(b => (b.HouseholdId, Amount: b.Balance.MinorUnits))
            .ToList();

        var transfers = new List<Transfer>();
        while (debtors.Count > 0 && creditors.Count > 0)
        {
            var debtor = Largest(debtors, order);
            var creditor = Largest(creditors, order);
            var amount = Math.Min(debtor.Amount, creditor.Amount);

            transfers.Add(new Transfer(
                debtor.HouseholdId,
                names[debtor.HouseholdId],
                creditor.HouseholdId,
                names[creditor.HouseholdId],
                Money.FromMinorUnits(amount)));

            Settle(debtors, debtor, amount);
            Settle(creditors, creditor, amount);
        }

        return transfers;
    }

    private static (Guid HouseholdId, long Amount) Largest(
        List<(Guid HouseholdId, long Amount)> items,
        IReadOnlyDictionary<Guid, int> order) =>
        items.OrderByDescending(i => i.Amount).ThenBy(i => order[i.HouseholdId]).First();

    private static void Settle(List<(Guid HouseholdId, long Amount)> items, (Guid HouseholdId, long Amount) item, long amount)
    {
        items.Remove(item);
        if (item.Amount > amount)
        {
            items.Add((item.HouseholdId, item.Amount - amount));
        }
    }
}
