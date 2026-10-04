using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace FamilyExpenses.Infrastructure.Persistence.Repositories;

internal sealed class ExpenseEventRepository(AppDbContext db) : IExpenseEventRepository
{
    public Task<ExpenseEvent?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        WithAggregate().SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ExpenseEvent>> ListForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var eventIds = db.EventMembers.Where(m => m.UserId == userId).Select(m => m.EventId);
        var events = await WithAggregate()
            .Where(e => eventIds.Contains(e.Id))
            .ToListAsync(cancellationToken);

        return [.. events.OrderBy(e => e.Name, StringComparer.CurrentCulture)];
    }

    public void Add(ExpenseEvent expenseEvent) => db.Events.Add(expenseEvent);

    public void Remove(ExpenseEvent expenseEvent) => db.Events.Remove(expenseEvent);

    private IQueryable<ExpenseEvent> WithAggregate() =>
        db.Events
            .Include(e => e.Households.OrderBy(h => h.SortOrder))
            .ThenInclude(h => h.Participants.OrderBy(p => p.SortOrder))
            .Include(e => e.Expenses)
            .Include(e => e.Payments)
            .Include(e => e.Approvals)
            .AsSplitQuery();
}
