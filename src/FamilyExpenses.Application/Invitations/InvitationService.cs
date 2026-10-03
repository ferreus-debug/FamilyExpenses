using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Common;
using FamilyExpenses.Domain.Access;
using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Application.Invitations;

/// <summary>Invitation links that tie a family's login to their household.</summary>
public sealed class InvitationService(
    IUnitOfWorkFactory uowFactory,
    ICurrentUser currentUser,
    IUserDirectory users,
    TimeProvider timeProvider)
{
    public async Task<CreatedInvitation> CreateAsync(
        Guid eventId,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, _) = await uow.LoadForAdminAsync(currentUser, eventId, cancellationToken);
        expenseEvent.GetHousehold(householdId);

        var (invitation, token) = Invitation.Create(eventId, householdId, timeProvider.GetUtcNow());
        uow.Invitations.Add(invitation);
        await uow.SaveChangesAsync(cancellationToken);
        return new CreatedInvitation(invitation.Id, token, invitation.ExpiresAt);
    }

    public async Task<IReadOnlyList<InvitationDto>> ListAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, _) = await uow.LoadForAdminAsync(currentUser, eventId, cancellationToken);
        var invitations = await uow.Invitations.ListForEventAsync(eventId, cancellationToken);
        var names = await users.GetDisplayNamesAsync(
            invitations.Where(i => i.AcceptedByUserId is not null).Select(i => i.AcceptedByUserId!),
            cancellationToken);
        var now = timeProvider.GetUtcNow();

        return
        [
            .. invitations
                .OrderByDescending(i => i.ExpiresAt)
                .Select(i => new InvitationDto(
                    i.Id,
                    i.HouseholdId,
                    expenseEvent.Households.SingleOrDefault(h => h.Id == i.HouseholdId)?.Name ?? "(fjernet)",
                    i.ExpiresAt,
                    i.IsAccepted ? InvitationStatus.Accepted
                        : i.IsValid(now) ? InvitationStatus.Pending
                        : InvitationStatus.Expired,
                    i.AcceptedByUserId is { } userId ? names.GetValueOrDefault(userId, "Ukendt") : null)),
        ];
    }

    /// <summary>Anonymous: shows which event and household a link is for. <c>null</c> if the link is unknown.</summary>
    public async Task<InvitationPreview?> PreviewAsync(string token, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var invitation = await uow.Invitations.GetByTokenAsync(token, cancellationToken);
        if (invitation is null)
        {
            return null;
        }

        var expenseEvent = await uow.Events.GetAsync(invitation.EventId, cancellationToken);
        var household = expenseEvent?.Households.SingleOrDefault(h => h.Id == invitation.HouseholdId);
        if (expenseEvent is null || household is null)
        {
            return null;
        }

        return new InvitationPreview(expenseEvent.Name, household.Name, invitation.IsValid(timeProvider.GetUtcNow()));
    }

    /// <summary>Joins the logged-in user to the invited household. Returns the event id.</summary>
    public async Task<Guid> AcceptAsync(string token, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        await using var uow = uowFactory.Create();
        var invitation = await uow.Invitations.GetByTokenAsync(token, cancellationToken)
            ?? throw new NotFoundException("Invitationen findes ikke.");

        var expenseEvent = await uow.Events.GetAsync(invitation.EventId, cancellationToken)
            ?? throw new NotFoundException("Begivenheden findes ikke længere.");
        if (expenseEvent.Households.All(h => h.Id != invitation.HouseholdId))
        {
            throw new DomainException("Husstanden i invitationen findes ikke længere.");
        }

        if (await uow.Members.GetAsync(invitation.EventId, userId, cancellationToken) is not null)
        {
            throw new DomainException("Du er allerede med i denne begivenhed.");
        }

        uow.Members.Add(invitation.Accept(userId, timeProvider.GetUtcNow()));
        await uow.SaveChangesAsync(cancellationToken);
        return invitation.EventId;
    }
}
