namespace FamilyExpenses.Application.Abstractions;

/// <summary>
/// One business operation against the database: load aggregates, change them, save once.
/// Blazor Server circuits are long-lived, so each operation gets its own short-lived unit of work.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IExpenseEventRepository Events { get; }

    IEventMemberRepository Members { get; }

    IInvitationRepository Invitations { get; }

    IExpensePictureRepository Pictures { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();
}
