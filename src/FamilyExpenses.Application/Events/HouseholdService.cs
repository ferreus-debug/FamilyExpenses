using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Common;
using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Application.Events;

/// <summary>
/// Households and participants. The administrator manages the structure (families, extra person);
/// each family may maintain its own participants.
/// </summary>
public sealed class HouseholdService(IUnitOfWorkFactory uowFactory, ICurrentUser currentUser)
{
    public async Task<Guid> AddFamilyAsync(Guid eventId, string name, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, _) = await uow.LoadForAdminAsync(currentUser, eventId, cancellationToken);
        var family = expenseEvent.AddFamily(name);
        await uow.SaveChangesAsync(cancellationToken);
        return family.Id;
    }

    public async Task RenameHouseholdAsync(
        Guid eventId,
        Guid householdId,
        string name,
        CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        EventAccess.RequireHouseholdAccess(member, householdId);
        expenseEvent.RenameHousehold(householdId, name);
        await uow.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveFamilyAsync(Guid eventId, Guid householdId, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, _) = await uow.LoadForAdminAsync(currentUser, eventId, cancellationToken);
        await EnsureNoLoginsAsync(uow, eventId, householdId, cancellationToken);
        expenseEvent.RemoveFamily(householdId);
        await uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> SetExtraPersonAsync(
        Guid eventId,
        string name,
        ParticipantType type,
        CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, _) = await uow.LoadForAdminAsync(currentUser, eventId, cancellationToken);
        var household = expenseEvent.SetExtraPerson(name, type);
        await uow.SaveChangesAsync(cancellationToken);
        return household.Id;
    }

    public async Task RemoveExtraPersonAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, _) = await uow.LoadForAdminAsync(currentUser, eventId, cancellationToken);
        if (expenseEvent.ExtraPerson is { } extra)
        {
            await EnsureNoLoginsAsync(uow, eventId, extra.Id, cancellationToken);
        }

        expenseEvent.RemoveExtraPerson();
        await uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> AddParticipantAsync(
        Guid eventId,
        Guid familyId,
        string name,
        ParticipantType type,
        CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        EventAccess.RequireHouseholdAccess(member, familyId);
        var participant = expenseEvent.AddParticipant(familyId, name, type);
        await uow.SaveChangesAsync(cancellationToken);
        return participant.Id;
    }

    public async Task UpdateParticipantAsync(
        Guid eventId,
        Guid participantId,
        string name,
        ParticipantType type,
        CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        EventAccess.RequireHouseholdAccess(member, expenseEvent.GetParticipant(participantId).HouseholdId);
        expenseEvent.UpdateParticipant(participantId, name, type);
        await uow.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveParticipantAsync(Guid eventId, Guid participantId, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        EventAccess.RequireHouseholdAccess(member, expenseEvent.GetParticipant(participantId).HouseholdId);
        expenseEvent.RemoveParticipant(participantId);
        await uow.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureNoLoginsAsync(
        IUnitOfWork uow,
        Guid eventId,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var members = await uow.Members.ListForEventAsync(eventId, cancellationToken);
        if (members.Any(m => m.HouseholdId == householdId))
        {
            throw new DomainException("Husstanden har brugere tilknyttet og kan ikke fjernes.");
        }
    }
}
