using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FamilyExpenses.Infrastructure.Persistence;

internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public IExpenseEventRepository Events { get; } = new ExpenseEventRepository(db);

    public IEventMemberRepository Members { get; } = new EventMemberRepository(db);

    public IInvitationRepository Invitations { get; } = new InvitationRepository(db);

    public IExpensePictureRepository Pictures { get; } = new ExpensePictureRepository(db);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);

    public ValueTask DisposeAsync() => db.DisposeAsync();
}

internal sealed class UnitOfWorkFactory(IDbContextFactory<AppDbContext> contextFactory) : IUnitOfWorkFactory
{
    public IUnitOfWork Create() => new UnitOfWork(contextFactory.CreateDbContext());
}
