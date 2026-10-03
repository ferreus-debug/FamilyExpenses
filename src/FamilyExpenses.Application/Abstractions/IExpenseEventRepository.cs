using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Application.Abstractions;

public interface IExpenseEventRepository
{
    /// <summary>Loads the full aggregate (households, participants, expenses).</summary>
    Task<ExpenseEvent?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Events the user is a member of, ordered by name.</summary>
    Task<IReadOnlyList<ExpenseEvent>> ListForUserAsync(string userId, CancellationToken cancellationToken = default);

    void Add(ExpenseEvent expenseEvent);

    void Remove(ExpenseEvent expenseEvent);
}
