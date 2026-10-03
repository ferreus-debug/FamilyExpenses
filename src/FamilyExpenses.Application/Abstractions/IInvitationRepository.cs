using FamilyExpenses.Domain.Access;

namespace FamilyExpenses.Application.Abstractions;

public interface IInvitationRepository
{
    /// <summary>Finds an invitation by the raw token from the link (looked up by its hash).</summary>
    Task<Invitation?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Invitation>> ListForEventAsync(Guid eventId, CancellationToken cancellationToken = default);

    void Add(Invitation invitation);
}
