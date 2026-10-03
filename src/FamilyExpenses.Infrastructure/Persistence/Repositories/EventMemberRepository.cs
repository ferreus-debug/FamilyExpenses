using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace FamilyExpenses.Infrastructure.Persistence.Repositories;

internal sealed class EventMemberRepository(AppDbContext db) : IEventMemberRepository
{
    public Task<EventMember?> GetAsync(Guid eventId, string userId, CancellationToken cancellationToken = default) =>
        db.EventMembers.SingleOrDefaultAsync(m => m.EventId == eventId && m.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<EventMember>> ListForEventAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        await db.EventMembers.Where(m => m.EventId == eventId).ToListAsync(cancellationToken);

    public void Add(EventMember member) => db.EventMembers.Add(member);

    public void Remove(EventMember member) => db.EventMembers.Remove(member);
}
