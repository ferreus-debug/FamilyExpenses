using FamilyExpenses.Application.Common;
using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;
using FamilyExpenses.Tests.Shared;

namespace FamilyExpenses.Infrastructure.Tests;

/// <summary>Two people saving the same event at once: the second save must fail instead of silently winning.</summary>
public sealed class ConcurrencyTests : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 7, 1);
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 10, 0, 0, TimeSpan.Zero);

    private TestDatabase _db = null!;

    public async Task InitializeAsync() => _db = await TestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Last_two_households_approving_at_once_cannot_both_win()
    {
        var expenseEvent = CreateEvent();
        expenseEvent.Close();
        var households = expenseEvent.HouseholdsToApprove.Select(h => h.Id).ToList();
        foreach (var id in households.SkipLast(2))
        {
            expenseEvent.Approve(id, "u1", Now);
        }

        await SaveNewAsync(expenseEvent);

        await using var first = _db.UnitOfWork.Create();
        await using var second = _db.UnitOfWork.Create();
        var seenByFirst = (await first.Events.GetAsync(expenseEvent.Id)).ShouldNotBeNull();
        var seenBySecond = (await second.Events.GetAsync(expenseEvent.Id)).ShouldNotBeNull();
        seenByFirst.Approve(households[^2], "u2", Now);
        seenBySecond.Approve(households[^1], "u3", Now);

        await first.SaveChangesAsync();
        var conflict = await Should.ThrowAsync<ConcurrencyConflictException>(() => second.SaveChangesAsync());
        conflict.Message.ShouldBe("Nogen har lige ændret turen – prøv igen.");

        // Nothing from the losing save was written, and retrying on fresh data settles the event.
        var reloaded = await LoadAsync(expenseEvent.Id);
        reloaded.Status.ShouldBe(EventStatus.Closed);
        reloaded.IsApproved(households[^1]).ShouldBeFalse();

        await using (var retry = _db.UnitOfWork.Create())
        {
            var fresh = (await retry.Events.GetAsync(expenseEvent.Id)).ShouldNotBeNull();
            fresh.Approve(households[^1], "u3", Now);
            await retry.SaveChangesAsync();
        }

        (await LoadAsync(expenseEvent.Id)).Status.ShouldBe(EventStatus.Settled);
    }

    [Fact]
    public async Task Same_household_approving_twice_at_once_is_a_conflict()
    {
        var expenseEvent = CreateEvent();
        expenseEvent.Close();
        await SaveNewAsync(expenseEvent);
        var household = expenseEvent.Households[0].Id;

        await using var first = _db.UnitOfWork.Create();
        await using var second = _db.UnitOfWork.Create();
        (await first.Events.GetAsync(expenseEvent.Id)).ShouldNotBeNull().Approve(household, "u1", Now);
        (await second.Events.GetAsync(expenseEvent.Id)).ShouldNotBeNull().Approve(household, "u2", Now);

        await first.SaveChangesAsync();
        await Should.ThrowAsync<ConcurrencyConflictException>(() => second.SaveChangesAsync());

        (await LoadAsync(expenseEvent.Id)).Approvals.ShouldHaveSingleItem().ApprovedByUserId.ShouldBe("u1");
    }

    [Fact]
    public async Task Expense_added_while_the_trip_is_being_closed_is_refused()
    {
        var expenseEvent = CreateEvent();
        await SaveNewAsync(expenseEvent);
        var payer = expenseEvent.Participants.First().Id;

        await using var admin = _db.UnitOfWork.Create();
        await using var member = _db.UnitOfWork.Create();
        (await admin.Events.GetAsync(expenseEvent.Id)).ShouldNotBeNull().Close();
        (await member.Events.GetAsync(expenseEvent.Id)).ShouldNotBeNull()
            .AddExpense("Is", new Money(40), Today, payer, null, "u2");

        await admin.SaveChangesAsync();
        await Should.ThrowAsync<ConcurrencyConflictException>(() => member.SaveChangesAsync());

        var reloaded = await LoadAsync(expenseEvent.Id);
        reloaded.Status.ShouldBe(EventStatus.Closed);
        reloaded.Expenses.ShouldBeEmpty();
    }

    [Fact]
    public async Task Changes_to_participants_count_as_changes_to_the_event()
    {
        var expenseEvent = CreateEvent();
        await SaveNewAsync(expenseEvent);
        var participant = expenseEvent.Participants.First();

        await using var first = _db.UnitOfWork.Create();
        await using var second = _db.UnitOfWork.Create();
        (await first.Events.GetAsync(expenseEvent.Id)).ShouldNotBeNull()
            .UpdateParticipant(participant.Id, "Annette", ParticipantType.Adult);
        (await second.Events.GetAsync(expenseEvent.Id)).ShouldNotBeNull()
            .UpdateParticipant(participant.Id, "Anne", ParticipantType.Child);

        await first.SaveChangesAsync();
        await Should.ThrowAsync<ConcurrencyConflictException>(() => second.SaveChangesAsync());
        (await LoadAsync(expenseEvent.Id)).GetParticipant(participant.Id).Name.ShouldBe("Annette");
    }

    [Fact]
    public async Task Saves_one_after_the_other_all_succeed()
    {
        var expenseEvent = CreateEvent();
        await SaveNewAsync(expenseEvent);
        var payer = expenseEvent.Participants.First().Id;

        for (var i = 0; i < 3; i++)
        {
            await using var uow = _db.UnitOfWork.Create();
            (await uow.Events.GetAsync(expenseEvent.Id)).ShouldNotBeNull()
                .AddExpense($"Mad {i}", new Money(10), Today, payer, null, "u1");
            await uow.SaveChangesAsync();
        }

        (await LoadAsync(expenseEvent.Id)).Expenses.Count.ShouldBe(3);
    }

    private static ExpenseEvent CreateEvent()
    {
        var expenseEvent = new ExpenseEvent("Sommerhus 2026");
        foreach (var (family, person) in new[] { ("Familie A", "Anna"), ("Familie B", "Bo"), ("Familie C", "Carla") })
        {
            var household = expenseEvent.AddFamily(family);
            expenseEvent.AddParticipant(household.Id, person, ParticipantType.Adult);
        }

        expenseEvent.SetExtraPerson("Xenia", ParticipantType.Adult);
        return expenseEvent;
    }

    private async Task SaveNewAsync(ExpenseEvent expenseEvent)
    {
        await using var uow = _db.UnitOfWork.Create();
        uow.Events.Add(expenseEvent);
        await uow.SaveChangesAsync();
    }

    private async Task<ExpenseEvent> LoadAsync(Guid id)
    {
        await using var uow = _db.UnitOfWork.Create();
        return (await uow.Events.GetAsync(id)).ShouldNotBeNull();
    }
}
