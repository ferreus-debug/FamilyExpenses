using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Common;
using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Application.Settlements;

public sealed class SettlementService(IUnitOfWorkFactory uowFactory, ICurrentUser currentUser)
{
    public async Task<SettlementDto> GetAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, _) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        var settlement = expenseEvent.CalculateSettlement();

        var paidByPerson = expenseEvent.Expenses
            .GroupBy(e => e.PaidByParticipantId)
            .ToDictionary(g => g.Key, g => g.Aggregate(Money.Zero, (sum, e) => sum + e.Amount));

        var balances = settlement.Balances
            .Select(b => new HouseholdBalanceDto(
                b.HouseholdId,
                b.Name,
                b.Weight.Value,
                b.Paid.Amount,
                b.Share.Amount,
                b.Balance.Amount,
                [.. expenseEvent.GetHousehold(b.HouseholdId).Participants
                    .OrderBy(p => p.SortOrder)
                    .Where(p => paidByPerson.ContainsKey(p.Id))
                    .Select(p => new PersonPaidDto(p.Id, p.Name, paidByPerson[p.Id].Amount))]))
            .ToList();

        return new SettlementDto(
            settlement.Total.Amount,
            expenseEvent.Households.Select(h => h.TotalWeight).Sum().Value,
            expenseEvent.IsSettled,
            balances,
            [.. settlement.Transfers.Select(t => new TransferDto(t.FromHouseholdId, t.FromName, t.ToHouseholdId, t.ToName, t.Amount.Amount))]);
    }
}
