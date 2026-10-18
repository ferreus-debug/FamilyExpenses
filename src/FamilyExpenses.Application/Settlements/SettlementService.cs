using System.Globalization;
using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Common;
using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;

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
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

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
        var money = new Money(amount);
        EnsureNotMoreThanOutstanding(expenseEvent, fromHouseholdId, toHouseholdId, money);
        var payment = expenseEvent.RecordPayment(
            fromHouseholdId,
            toHouseholdId,
            money,
            DateOnly.FromDateTime(time.GetLocalNow().DateTime),
            member.UserId,
            time.GetUtcNow());
        await uow.SaveChangesAsync(cancellationToken);
        return payment.Id;
    }

    /// <summary>
    /// The page sends the transfer it showed, which may be stale: if someone else in the household has just
    /// marked it as paid, recording it again would pay twice and flip the balances. So the amount is checked
    /// against what is still outstanding right now.
    /// </summary>
    private static void EnsureNotMoreThanOutstanding(ExpenseEvent expenseEvent, Guid fromHouseholdId, Guid toHouseholdId, Money amount)
    {
        var outstanding = expenseEvent.CalculateSettlement().Transfers
            .Where(t => t.FromHouseholdId == fromHouseholdId && t.ToHouseholdId == toHouseholdId)
            .Aggregate(Money.Zero, (sum, t) => sum + t.Amount);
        if (amount > outstanding)
        {
            var from = expenseEvent.GetHousehold(fromHouseholdId).Name;
            var to = expenseEvent.GetHousehold(toHouseholdId).Name;
            throw new DomainException(outstanding == Money.Zero
                ? $"{from} skylder ikke {to} noget – måske har en anden lige registreret betalingen. Opdatér siden."
                : $"{from} skylder kun {to} {outstanding.Amount.ToString("N2", Danish)} {expenseEvent.Currency} – måske har en anden lige registreret en betaling. Opdatér siden.");
        }
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
