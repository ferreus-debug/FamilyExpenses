using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Domain.Access;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Web.Tests;

internal sealed class StaticCurrentUser(string? userId) : ICurrentUser
{
    public string? UserId { get; } = userId;
}

internal sealed class EmptyUserDirectory : IUserDirectory
{
    public Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
}

/// <summary>A database with no events, for rendering pages in their empty state.</summary>
internal sealed class EmptyUnitOfWorkFactory : IUnitOfWorkFactory
{
    public IUnitOfWork Create() => new EmptyUnitOfWork();

    private sealed class EmptyUnitOfWork : IUnitOfWork, IExpenseEventRepository, IEventMemberRepository, IInvitationRepository
    {
        public IExpenseEventRepository Events => this;

        public IEventMemberRepository Members => this;

        public IInvitationRepository Invitations => this;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        Task<ExpenseEvent?> IExpenseEventRepository.GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<ExpenseEvent?>(null);

        Task<IReadOnlyList<ExpenseEvent>> IExpenseEventRepository.ListForUserAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExpenseEvent>>([]);

        void IExpenseEventRepository.Add(ExpenseEvent expenseEvent)
        {
        }

        void IExpenseEventRepository.Remove(ExpenseEvent expenseEvent)
        {
        }

        Task<EventMember?> IEventMemberRepository.GetAsync(Guid eventId, string userId, CancellationToken cancellationToken) =>
            Task.FromResult<EventMember?>(null);

        Task<IReadOnlyList<EventMember>> IEventMemberRepository.ListForEventAsync(Guid eventId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EventMember>>([]);

        void IEventMemberRepository.Add(EventMember member)
        {
        }

        void IEventMemberRepository.Remove(EventMember member)
        {
        }

        Task<Invitation?> IInvitationRepository.GetByTokenAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult<Invitation?>(null);

        Task<IReadOnlyList<Invitation>> IInvitationRepository.ListForEventAsync(Guid eventId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Invitation>>([]);

        void IInvitationRepository.Add(Invitation invitation)
        {
        }
    }
}
