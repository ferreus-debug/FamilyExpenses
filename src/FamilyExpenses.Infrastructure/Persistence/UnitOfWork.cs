using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Common;
using FamilyExpenses.Domain.Events;
using FamilyExpenses.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FamilyExpenses.Infrastructure.Persistence;

internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    // SQLITE_CONSTRAINT and SQLITE_CONSTRAINT_UNIQUE.
    private const int ConstraintError = 19;
    private const int UniqueConstraintError = 2067;

    public IExpenseEventRepository Events { get; } = new ExpenseEventRepository(db);

    public IEventMemberRepository Members { get; } = new EventMemberRepository(db);

    public IInvitationRepository Invitations { get; } = new InvitationRepository(db);

    public IExpensePictureRepository Pictures { get; } = new ExpensePictureRepository(db);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ConcurrencyConflictException.DefaultMessage, ex);
        }
        catch (DbUpdateException ex) when (ex.Entries.Any(e => e.Entity is HouseholdApproval) && IsUniqueViolation(ex))
        {
            // The same household approved twice at once: the unique index stopped the second one.
            throw new ConcurrencyConflictException(ConcurrencyConflictException.DefaultMessage, ex);
        }
    }

    public ValueTask DisposeAsync() => db.DisposeAsync();

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteErrorCode: ConstraintError, SqliteExtendedErrorCode: UniqueConstraintError };
}

internal sealed class UnitOfWorkFactory(IDbContextFactory<AppDbContext> contextFactory) : IUnitOfWorkFactory
{
    public IUnitOfWork Create() => new UnitOfWork(contextFactory.CreateDbContext());
}
