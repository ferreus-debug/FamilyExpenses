using FamilyExpenses.Domain.Access;

namespace FamilyExpenses.Application.Abstractions;

public interface IEventMemberRepository
{
    Task<EventMember?> GetAsync(Guid eventId, string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventMember>> ListForEventAsync(Guid eventId, CancellationToken cancellationToken = default);

    void Add(EventMember member);

    void Remove(EventMember member);
}
