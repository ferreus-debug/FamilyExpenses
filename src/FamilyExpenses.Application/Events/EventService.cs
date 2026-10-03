using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Common;
using FamilyExpenses.Domain.Access;
using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Application.Events;

public sealed class EventService(IUnitOfWorkFactory uowFactory, ICurrentUser currentUser, IUserDirectory users)
{
    /// <summary>Creates an event; the creator becomes its administrator.</summary>
    public async Task<Guid> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        var expenseEvent = new ExpenseEvent(name);

        await using var uow = uowFactory.Create();
        uow.Events.Add(expenseEvent);
        uow.Members.Add(new EventMember(expenseEvent.Id, userId, householdId: null, MemberRole.Admin));
        await uow.SaveChangesAsync(cancellationToken);
        return expenseEvent.Id;
    }

    public async Task<IReadOnlyList<EventSummary>> ListMineAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        await using var uow = uowFactory.Create();
        var events = await uow.Events.ListForUserAsync(userId, cancellationToken);
        var result = new List<EventSummary>(events.Count);
        foreach (var expenseEvent in events)
        {
            var member = (await uow.Members.GetAsync(expenseEvent.Id, userId, cancellationToken))!;
            var myHousehold = expenseEvent.Households.SingleOrDefault(h => h.Id == member.HouseholdId);
            var total = expenseEvent.Expenses.Aggregate(Money.Zero, (sum, e) => sum + e.Amount);
            result.Add(new EventSummary(
                expenseEvent.Id,
                expenseEvent.Name,
                expenseEvent.IsSettled,
                member.IsAdmin,
                myHousehold?.Name,
                total.Amount));
        }

        return result;
    }

    public async Task<EventDetails> GetAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        var members = await uow.Members.ListForEventAsync(eventId, cancellationToken);
        var names = await users.GetDisplayNamesAsync(members.Select(m => m.UserId), cancellationToken);

        var households = expenseEvent.Households
            .OrderBy(h => h.SortOrder)
            .Select(h => new HouseholdDto(
                h.Id,
                h.Name,
                h.Kind,
                h.TotalWeight.Value,
                [.. h.Participants
                    .OrderBy(p => p.SortOrder)
                    .Select(p => new ParticipantDto(p.Id, p.Name, p.Type, p.Weight.Value))],
                [.. members
                    .Where(m => m.HouseholdId == h.Id)
                    .Select(m => names.GetValueOrDefault(m.UserId, "Ukendt"))
                    .Order(StringComparer.CurrentCulture)]))
            .ToList();

        return new EventDetails(
            expenseEvent.Id,
            expenseEvent.Name,
            expenseEvent.Currency,
            expenseEvent.IsSettled,
            member.IsAdmin,
            member.HouseholdId,
            ExpenseEvent.MaxFamilies,
            households);
    }

    public Task RenameAsync(Guid eventId, string name, CancellationToken cancellationToken = default) =>
        AsAdminAsync(eventId, (e, _) => e.Rename(name), cancellationToken);

    public Task MarkSettledAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        AsAdminAsync(eventId, (e, _) => e.MarkSettled(), cancellationToken);

    public Task ReopenAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        AsAdminAsync(eventId, (e, _) => e.Reopen(), cancellationToken);

    public async Task DeleteAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, _) = await uow.LoadForAdminAsync(currentUser, eventId, cancellationToken);
        uow.Events.Remove(expenseEvent);
        await uow.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Lets the administrator say which household they belong to themselves.</summary>
    public async Task JoinHouseholdAsync(Guid eventId, Guid householdId, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForAdminAsync(currentUser, eventId, cancellationToken);
        expenseEvent.GetHousehold(householdId);
        member.AssignHousehold(householdId);
        await uow.SaveChangesAsync(cancellationToken);
    }

    private async Task AsAdminAsync(
        Guid eventId,
        Action<ExpenseEvent, EventMember> change,
        CancellationToken cancellationToken)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForAdminAsync(currentUser, eventId, cancellationToken);
        change(expenseEvent, member);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
