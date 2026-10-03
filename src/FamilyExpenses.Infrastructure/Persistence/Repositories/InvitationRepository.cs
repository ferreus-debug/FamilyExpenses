using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace FamilyExpenses.Infrastructure.Persistence.Repositories;

internal sealed class InvitationRepository(AppDbContext db) : IInvitationRepository
{
    public Task<Invitation?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var hash = Invitation.HashToken(token);
        return db.Invitations.SingleOrDefaultAsync(i => i.TokenHash == hash, cancellationToken);
    }

    public async Task<IReadOnlyList<Invitation>> ListForEventAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        await db.Invitations.Where(i => i.EventId == eventId).ToListAsync(cancellationToken);

    public void Add(Invitation invitation) => db.Invitations.Add(invitation);
}
