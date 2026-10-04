using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Common;
using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Application.Settlements;

/// <summary>
/// The settlement, and the payments households register as they pay each other. Only the two households
/// in a payment (or the admin) may register or remove it.
/// </summary>
public sealed class SettlementService(
    IUnitOfWorkFactory uowFactory,
    ICurrentUser currentUser,
    IUserDirectory users,
    TimeProvider time)
{
    public async Task<SettlementDto> GetAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
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
                b.Transferred.Amount,
                b.Balance.Amount,
                [.. expenseEvent.GetHousehold(b.HouseholdId).Participants
                    .OrderBy(p => p.SortOrder)
                    .Where(p => paidByPerson.ContainsKey(p.Id))
                    .Select(p => new PersonPaidDto(p.Id, p.Name, paidByPerson[p.Id].Amount))],
                [.. b.Expenses.Select(e => new ExpenseShareDto(
                    e.ExpenseId,
                    e.Description,
                    e.Date,
                    e.Paid.Amount,
                    e.Share.Amount,
                    e.HouseholdWeight?.Value,
                    e.SharedWeight.Value))]))
            .ToList();

        var names = await users.GetDisplayNamesAsync(
            expenseEvent.Payments.Select(p => p.CreatedByUserId).Distinct(),
            cancellationToken);
        var payments = expenseEvent.Payments
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PaymentDto(
                p.Id,
                p.FromHouseholdId,
                expenseEvent.GetHousehold(p.FromHouseholdId).Name,
                p.ToHouseholdId,
                expenseEvent.GetHousehold(p.ToHouseholdId).Name,
                p.Amount.Amount,
                p.Date,
                names.GetValueOrDefault(p.CreatedByUserId, "Ukendt"),
                EventAccess.CanHandlePayment(member, p.FromHouseholdId, p.ToHouseholdId)))
            .ToList();

        return new SettlementDto(
            settlement.Total.Amount,
            expenseEvent.Households.Select(h => h.TotalWeight).Sum().Value,
            expenseEvent.IsSettled,
            balances,
            [.. settlement.Transfers.Select(t => new TransferDto(
                t.FromHouseholdId,
                t.FromName,
                t.ToHouseholdId,
                t.ToName,
                t.Amount.Amount,
                expenseEvent.IsSettled && EventAccess.CanHandlePayment(member, t.FromHouseholdId, t.ToHouseholdId)))],
            payments);
    }

    /// <summary>Registers that one household has paid another, e.g. a suggested transfer.</summary>
    public async Task<Guid> RecordPaymentAsync(
        Guid eventId,
        Guid fromHouseholdId,
        Guid toHouseholdId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        EventAccess.RequirePaymentAccess(member, fromHouseholdId, toHouseholdId);
        var payment = expenseEvent.RecordPayment(
            fromHouseholdId,
            toHouseholdId,
            new Money(amount),
            DateOnly.FromDateTime(time.GetLocalNow().DateTime),
            member.UserId,
            time.GetUtcNow());
        await uow.SaveChangesAsync(cancellationToken);
        return payment.Id;
    }

    public async Task RemovePaymentAsync(Guid eventId, Guid paymentId, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        var payment = expenseEvent.GetPayment(paymentId);
        EventAccess.RequirePaymentAccess(member, payment.FromHouseholdId, payment.ToHouseholdId);
        expenseEvent.RemovePayment(paymentId);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
