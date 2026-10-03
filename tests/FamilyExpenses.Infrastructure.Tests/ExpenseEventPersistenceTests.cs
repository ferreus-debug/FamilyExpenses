using FamilyExpenses.Domain.Access;
using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;
using FamilyExpenses.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace FamilyExpenses.Infrastructure.Tests;

public sealed class ExpenseEventPersistenceTests : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 7, 1);

    private TestDatabase _db = null!;

    public async Task InitializeAsync() => _db = await TestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Migrations_match_the_model()
    {
        await using var context = _db.CreateContext();

        context.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    [Fact]
    public async Task Aggregate_round_trips_with_households_participants_and_expenses()
    {
        var (expenseEvent, anna, bo, carla) = CreateEvent();
        expenseEvent.AddExpense("Sommerhus", new Money(4250), Today, anna.Id, null, "u1");
        expenseEvent.AddExpense("Restaurant", new Money(300.55m), Today, anna.Id, [bo.Id, carla.Id], "u2");
        var expected = expenseEvent.CalculateSettlement();
        await SaveNewAsync(expenseEvent);

        var loaded = await LoadAsync(expenseEvent.Id);

        loaded.Name.ShouldBe("Sommerhus 2026");
        loaded.Currency.ShouldBe("DKK");
        loaded.Households.Select(h => h.Name).ShouldBe(["Familie A", "Familie B", "Familie C", "Xenia"]);
        loaded.Families.Single(f => f.Name == "Familie C").Participants.Select(p => p.Name).ShouldBe(["Carla", "Cilius"]);
        loaded.ExtraPerson.ShouldNotBeNull().Participants.ShouldHaveSingleItem().Type.ShouldBe(ParticipantType.Child);
        loaded.Participants.Count().ShouldBe(5);

        var restaurant = loaded.Expenses.Single(e => e.Description == "Restaurant");
        restaurant.Amount.ShouldBe(new Money(300.55m));
        restaurant.Date.ShouldBe(Today);
        restaurant.PaidByParticipantId.ShouldBe(anna.Id);
        restaurant.SharedWithParticipantIds.ShouldBe([bo.Id, carla.Id], ignoreOrder: true);
        restaurant.CreatedByUserId.ShouldBe("u2");

        var actual = loaded.CalculateSettlement();
        actual.Total.ShouldBe(expected.Total);
        actual.Balances.ShouldBe(expected.Balances);
        actual.Transfers.ShouldBe(expected.Transfers);
    }

    [Fact]
    public async Task Changes_to_a_loaded_aggregate_are_saved()
    {
        var (expenseEvent, anna, bo, _) = CreateEvent();
        var expense = expenseEvent.AddExpense("Mad", new Money(100), Today, anna.Id, null, "u1");
        await SaveNewAsync(expenseEvent);

        await using (var uow = _db.UnitOfWork.Create())
        {
            var loaded = (await uow.Events.GetAsync(expenseEvent.Id)).ShouldNotBeNull();
            loaded.UpdateExpense(expense.Id, "Aftensmad", new Money(150), Today, bo.Id, [bo.Id]);
            loaded.AddExpense("Is", new Money(40), Today, bo.Id, null, "u2");
            loaded.SetExtraPerson("Mormor", ParticipantType.Adult);
            var familyC = loaded.Families.Single(f => f.Name == "Familie C");
            loaded.AddParticipant(familyC.Id, "Cille", ParticipantType.Child);
            loaded.MarkSettled();
            await uow.SaveChangesAsync();
        }

        var reloaded = await LoadAsync(expenseEvent.Id);
        reloaded.IsSettled.ShouldBeTrue();
        reloaded.Expenses.Count.ShouldBe(2);
        var updated = reloaded.GetExpense(expense.Id);
        updated.Description.ShouldBe("Aftensmad");
        updated.PaidByParticipantId.ShouldBe(bo.Id);
        updated.SharedWithParticipantIds.ShouldBe([bo.Id]);
        reloaded.ExtraPerson.ShouldNotBeNull().Name.ShouldBe("Mormor");
        reloaded.ExtraPerson!.TotalWeight.ShouldBe(Weight.Adult);
        reloaded.Participants.Count().ShouldBe(6);
    }

    [Fact]
    public async Task Removed_children_are_deleted()
    {
        var (expenseEvent, anna, _, carla) = CreateEvent();
        var expense = expenseEvent.AddExpense("Mad", new Money(100), Today, anna.Id, [anna.Id], "u1");
        await SaveNewAsync(expenseEvent);

        await using (var uow = _db.UnitOfWork.Create())
        {
            var loaded = (await uow.Events.GetAsync(expenseEvent.Id)).ShouldNotBeNull();
            loaded.RemoveExpense(expense.Id);
            loaded.RemoveParticipant(carla.Id);
            loaded.RemoveExtraPerson();
            await uow.SaveChangesAsync();
        }

        var reloaded = await LoadAsync(expenseEvent.Id);
        reloaded.Expenses.ShouldBeEmpty();
        reloaded.ExtraPerson.ShouldBeNull();
        reloaded.Participants.ShouldNotContain(p => p.Id == carla.Id);

        await using var context = _db.CreateContext();
        (await context.Set<Participant>().CountAsync()).ShouldBe(3);
        (await context.Set<Household>().CountAsync()).ShouldBe(3);
    }

    [Fact]
    public async Task Removing_an_event_cascades_to_everything_in_it()
    {
        var (expenseEvent, anna, _, _) = CreateEvent();
        expenseEvent.AddExpense("Mad", new Money(100), Today, anna.Id, null, "u1");
        await SaveNewAsync(expenseEvent);
        var userId = await AddUserAsync("anna@example.com");
        var (invitation, _) = Invitation.Create(expenseEvent.Id, expenseEvent.Households[0].Id, DateTimeOffset.UtcNow);

        await using (var uow = _db.UnitOfWork.Create())
        {
            uow.Members.Add(new EventMember(expenseEvent.Id, userId, null, MemberRole.Admin));
            uow.Invitations.Add(invitation);
            await uow.SaveChangesAsync();
        }

        await using (var uow = _db.UnitOfWork.Create())
        {
            uow.Events.Remove((await uow.Events.GetAsync(expenseEvent.Id)).ShouldNotBeNull());
            await uow.SaveChangesAsync();
        }

        await using var context = _db.CreateContext();
        (await context.Events.CountAsync()).ShouldBe(0);
        (await context.Set<Household>().CountAsync()).ShouldBe(0);
        (await context.Set<Participant>().CountAsync()).ShouldBe(0);
        (await context.Set<Expense>().CountAsync()).ShouldBe(0);
        (await context.EventMembers.CountAsync()).ShouldBe(0);
        (await context.Invitations.CountAsync()).ShouldBe(0);
        (await context.Users.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Lists_only_events_the_user_is_member_of()
    {
        var (mine, _, _, _) = CreateEvent("Sommerhus");
        var (other, _, _, _) = CreateEvent("Skiferie");
        await SaveNewAsync(mine);
        await SaveNewAsync(other);
        var userId = await AddUserAsync("bo@example.com");

        await using (var uow = _db.UnitOfWork.Create())
        {
            uow.Members.Add(new EventMember(mine.Id, userId, mine.Households[1].Id, MemberRole.Member));
            await uow.SaveChangesAsync();
        }

        await using (var uow = _db.UnitOfWork.Create())
        {
            var events = await uow.Events.ListForUserAsync(userId);
            events.ShouldHaveSingleItem().Id.ShouldBe(mine.Id);
            events[0].Households.Count.ShouldBe(4);

            var member = (await uow.Members.GetAsync(mine.Id, userId)).ShouldNotBeNull();
            member.HouseholdId.ShouldBe(mine.Households[1].Id);
            member.IsAdmin.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task User_can_only_be_member_once_per_event()
    {
        var (expenseEvent, _, _, _) = CreateEvent();
        await SaveNewAsync(expenseEvent);
        var userId = await AddUserAsync("dup@example.com");

        await using var uow = _db.UnitOfWork.Create();
        uow.Members.Add(new EventMember(expenseEvent.Id, userId, null, MemberRole.Admin));
        uow.Members.Add(new EventMember(expenseEvent.Id, userId, expenseEvent.Households[0].Id, MemberRole.Member));

        await Should.ThrowAsync<DbUpdateException>(() => uow.SaveChangesAsync());
    }

    [Fact]
    public async Task Invitation_is_found_by_raw_token_and_acceptance_is_saved()
    {
        var (expenseEvent, _, _, _) = CreateEvent();
        await SaveNewAsync(expenseEvent);
        var household = expenseEvent.Households[2];
        var now = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var (invitation, token) = Invitation.Create(expenseEvent.Id, household.Id, now);
        var userId = await AddUserAsync("carla@example.com");

        await using (var uow = _db.UnitOfWork.Create())
        {
            uow.Invitations.Add(invitation);
            await uow.SaveChangesAsync();
        }

        await using (var uow = _db.UnitOfWork.Create())
        {
            (await uow.Invitations.GetByTokenAsync("not-the-token")).ShouldBeNull();
            var loaded = (await uow.Invitations.GetByTokenAsync(token)).ShouldNotBeNull();
            loaded.ExpiresAt.ShouldBe(now + Invitation.DefaultLifetime);
            uow.Members.Add(loaded.Accept(userId, now.AddHours(1)));
            await uow.SaveChangesAsync();
        }

        await using (var uow = _db.UnitOfWork.Create())
        {
            var loaded = (await uow.Invitations.GetByTokenAsync(token)).ShouldNotBeNull();
            loaded.IsAccepted.ShouldBeTrue();
            loaded.AcceptedByUserId.ShouldBe(userId);
            (await uow.Members.GetAsync(expenseEvent.Id, userId)).ShouldNotBeNull().HouseholdId.ShouldBe(household.Id);
            (await uow.Invitations.ListForEventAsync(expenseEvent.Id)).Count.ShouldBe(1);
        }
    }

    private static (ExpenseEvent Event, Participant Anna, Participant Bo, Participant Carla) CreateEvent(
        string name = "Sommerhus 2026")
    {
        var expenseEvent = new ExpenseEvent(name);
        var a = expenseEvent.AddFamily("Familie A");
        var anna = expenseEvent.AddParticipant(a.Id, "Anna", ParticipantType.Adult);
        var b = expenseEvent.AddFamily("Familie B");
        var bo = expenseEvent.AddParticipant(b.Id, "Bo", ParticipantType.Adult);
        var c = expenseEvent.AddFamily("Familie C");
        var carla = expenseEvent.AddParticipant(c.Id, "Carla", ParticipantType.Adult);
        expenseEvent.AddParticipant(c.Id, "Cilius", ParticipantType.Child);
        expenseEvent.SetExtraPerson("Xenia", ParticipantType.Child);
        return (expenseEvent, anna, bo, carla);
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

    private Task<string> AddUserAsync(string email) => _db.AddUserAsync(email.Split('@')[0]);
}
