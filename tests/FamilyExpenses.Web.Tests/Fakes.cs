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

    private sealed class EmptyUnitOfWork : IUnitOfWork, IExpenseEventRepository, IEventMemberRepository, IInvitationRepository, IExpensePictureRepository
    {
        public IExpenseEventRepository Events => this;

        public IEventMemberRepository Members => this;

        public IInvitationRepository Invitations => this;

        public IExpensePictureRepository Pictures => this;

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

        Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> IExpensePictureRepository.ListIdsByExpenseAsync(Guid eventId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());

        Task<int> IExpensePictureRepository.CountForExpenseAsync(Guid expenseId, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        Task<PictureOwner?> IExpensePictureRepository.GetOwnerAsync(Guid pictureId, CancellationToken cancellationToken) =>
            Task.FromResult<PictureOwner?>(null);

        Task<PictureContent?> IExpensePictureRepository.GetContentAsync(Guid pictureId, bool thumbnail, CancellationToken cancellationToken) =>
            Task.FromResult<PictureContent?>(null);

        void IExpensePictureRepository.Add(ExpensePicture picture)
        {
        }

        Task IExpensePictureRepository.DeleteAsync(Guid pictureId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
