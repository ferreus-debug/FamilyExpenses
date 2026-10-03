using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Common;
using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Application.Expenses;

/// <summary>Any member may register expenses (also on behalf of others); only the creator or admin may change them.</summary>
public sealed class ExpenseService(IUnitOfWorkFactory uowFactory, ICurrentUser currentUser, IUserDirectory users)
{
    /// <summary>All expenses in the event, newest date first.</summary>
    public async Task<IReadOnlyList<ExpenseDto>> ListAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        var names = await users.GetDisplayNamesAsync(
            expenseEvent.Expenses.Select(e => e.CreatedByUserId).Distinct(),
            cancellationToken);

        return
        [
            .. expenseEvent.Expenses
                .OrderByDescending(e => e.Date)
                .ThenBy(e => e.Description, StringComparer.CurrentCulture)
                .Select(e =>
                {
                    var payer = expenseEvent.GetParticipant(e.PaidByParticipantId);
                    return new ExpenseDto(
                        e.Id,
                        e.Description,
                        e.Amount.Amount,
                        e.Date,
                        payer.Id,
                        payer.Name,
                        expenseEvent.GetHousehold(payer.HouseholdId).Name,
                        e.SharedWithParticipantIds,
                        DescribeSharedWith(expenseEvent, e),
                        names.GetValueOrDefault(e.CreatedByUserId, "Ukendt"),
                        EventAccess.CanEdit(member, e));
                }),
        ];
    }

    public async Task<Guid> AddAsync(Guid eventId, ExpenseInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        var expense = expenseEvent.AddExpense(
            input.Description,
            new Money(input.Amount),
            input.Date,
            input.PaidByParticipantId,
            input.SharedWithParticipantIds,
            member.UserId);
        await uow.SaveChangesAsync(cancellationToken);
        return expense.Id;
    }

    public async Task UpdateAsync(
        Guid eventId,
        Guid expenseId,
        ExpenseInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        EventAccess.RequireCanEdit(member, expenseEvent.GetExpense(expenseId));
        expenseEvent.UpdateExpense(
            expenseId,
            input.Description,
            new Money(input.Amount),
            input.Date,
            input.PaidByParticipantId,
            input.SharedWithParticipantIds);
        await uow.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid eventId, Guid expenseId, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        EventAccess.RequireCanEdit(member, expenseEvent.GetExpense(expenseId));
        expenseEvent.RemoveExpense(expenseId);
        await uow.SaveChangesAsync(cancellationToken);
    }

    internal static string DescribeSharedWith(ExpenseEvent expenseEvent, Expense expense)
    {
        var shared = expense.SharedWithParticipantIds.ToHashSet();
        if (expenseEvent.Participants.All(p => shared.Contains(p.Id)))
        {
            return "Alle";
        }

        var parts = new List<string>();
        foreach (var household in expenseEvent.Households.OrderBy(h => h.SortOrder))
        {
            var included = household.Participants.OrderBy(p => p.SortOrder).Where(p => shared.Contains(p.Id)).ToList();
            if (included.Count == 0)
            {
                continue;
            }

            if (included.Count == household.Participants.Count && household.Kind == HouseholdKind.Family)
            {
                parts.Add(household.Name);
            }
            else
            {
                parts.AddRange(included.Select(p => p.Name));
            }
        }

        return string.Join(", ", parts);
    }
}
