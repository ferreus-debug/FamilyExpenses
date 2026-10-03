using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Domain.Access;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Application.Common;

/// <summary>
/// The single place where access to an event is checked. Every service call goes through here,
/// so hiding a button in the UI is never the only protection.
/// </summary>
internal static class EventAccess
{
    public static string RequireUserId(this ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new ForbiddenException("Du skal være logget ind.");

    /// <summary>
    /// Loads the event for a member. Non-members get <see cref="NotFoundException"/> so the
    /// existence of other people's events is not revealed.
    /// </summary>
    public static async Task<(ExpenseEvent Event, EventMember Member)> LoadForMemberAsync(
        this IUnitOfWork uow,
        ICurrentUser currentUser,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var member = await uow.Members.GetAsync(eventId, userId, cancellationToken)
            ?? throw new NotFoundException("Begivenheden findes ikke.");
        var expenseEvent = await uow.Events.GetAsync(eventId, cancellationToken)
            ?? throw new NotFoundException("Begivenheden findes ikke.");
        return (expenseEvent, member);
    }

    public static async Task<(ExpenseEvent Event, EventMember Member)> LoadForAdminAsync(
        this IUnitOfWork uow,
        ICurrentUser currentUser,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var access = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        RequireAdmin(access.Member);
        return access;
    }

    public static void RequireAdmin(EventMember member)
    {
        if (!member.IsAdmin)
        {
            throw new ForbiddenException("Kun administratoren af begivenheden kan gøre dette.");
        }
    }

    /// <summary>Admins manage every household; members only their own.</summary>
    public static void RequireHouseholdAccess(EventMember member, Guid householdId)
    {
        if (!member.IsAdmin && member.HouseholdId != householdId)
        {
            throw new ForbiddenException("Du kan kun ændre din egen husstand.");
        }
    }

    public static bool CanEdit(EventMember member, Expense expense) =>
        member.IsAdmin || expense.CreatedByUserId == member.UserId;

    public static void RequireCanEdit(EventMember member, Expense expense)
    {
        if (!CanEdit(member, expense))
        {
            throw new ForbiddenException("Du kan kun ændre udgifter, du selv har oprettet.");
        }
    }
}
