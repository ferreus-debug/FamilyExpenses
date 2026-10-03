using FamilyExpenses.Application.Common;
using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Application.Tests;

public sealed class EventServiceTests : ServiceTestBase
{
    [Fact]
    public async Task Creator_is_admin_and_sees_the_full_event()
    {
        App.LogInAs(S.AnnaUser);

        var details = await App.Events.GetAsync(S.EventId);

        details.IsAdmin.ShouldBeTrue();
        details.MyHouseholdId.ShouldBe(S.A);
        details.Households.Select(h => h.Name).ShouldBe(["Familie A", "Familie B", "Familie C", "Xenia"]);
        details.Families.Count().ShouldBe(3);
        details.ExtraPerson.ShouldNotBeNull().Weight.ShouldBe(1.0m);
        details.CanAddFamily.ShouldBeFalse();
        details.Households.Single(h => h.Id == S.A).Participants.Select(p => p.Name).ShouldBe(["Anna", "Anders", "Alma", "Arne"]);
        details.Households.Single(h => h.Id == S.A).Weight.ShouldBe(3.0m);
    }

    [Fact]
    public async Task Members_are_shown_on_their_household()
    {
        App.LogInAs(S.BoUser);

        var details = await App.Events.GetAsync(S.EventId);

        details.IsAdmin.ShouldBeFalse();
        details.MyHouseholdId.ShouldBe(S.B);
        details.Households.Single(h => h.Id == S.A).MemberNames.ShouldBe(["Anna"]);
        details.Households.Single(h => h.Id == S.B).MemberNames.ShouldBe(["Bo"]);
        details.CanEditHousehold(S.B).ShouldBeTrue();
        details.CanEditHousehold(S.C).ShouldBeFalse();
    }

    [Fact]
    public async Task Lists_only_my_events_with_total_and_household()
    {
        App.LogInAs(S.BoUser);
        await App.Expenses.AddAsync(S.EventId, new("Mad", 120.50m, AppHarness.Today, S.Person("Bo"), null));

        var mine = await App.Events.ListMineAsync();

        mine.ShouldHaveSingleItem().ShouldBe(new(S.EventId, "Sommerhus 2026", false, false, "Familie B", 120.50m));

        App.LogInAs(S.DorteUser);
        (await App.Events.ListMineAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Admin_can_rename_settle_reopen_and_delete()
    {
        App.LogInAs(S.AnnaUser);

        await App.Events.RenameAsync(S.EventId, "Sommerhus Skagen");
        await App.Events.MarkSettledAsync(S.EventId);
        (await App.Events.GetAsync(S.EventId)).IsSettled.ShouldBeTrue();

        await App.Events.ReopenAsync(S.EventId);
        var details = await App.Events.GetAsync(S.EventId);
        details.Name.ShouldBe("Sommerhus Skagen");
        details.IsSettled.ShouldBeFalse();

        await App.Events.DeleteAsync(S.EventId);
        await Should.ThrowAsync<NotFoundException>(() => App.Events.GetAsync(S.EventId));
    }

    [Fact]
    public async Task Settled_event_rejects_changes_from_everyone()
    {
        App.LogInAs(S.AnnaUser);
        await App.Events.MarkSettledAsync(S.EventId);

        App.LogInAs(S.BoUser);
        await Should.ThrowAsync<DomainException>(
            () => App.Expenses.AddAsync(S.EventId, new("Is", 40, AppHarness.Today, S.Person("Bo"), null)));
        await Should.ThrowAsync<DomainException>(
            () => App.Households.AddParticipantAsync(S.EventId, S.B, "Ny", ParticipantType.Child));
    }

    [Fact]
    public async Task Family_with_logins_cannot_be_removed_but_an_empty_one_can()
    {
        App.LogInAs(S.AnnaUser);
        await Should.ThrowAsync<DomainException>(() => App.Households.RemoveFamilyAsync(S.EventId, S.B));

        var other = await App.Events.CreateAsync("Skiferie");
        var empty = await App.Households.AddFamilyAsync(other, "Familie Tom");
        await App.Households.RemoveFamilyAsync(other, empty);

        (await App.Events.GetAsync(other)).Households.ShouldBeEmpty();
    }

    [Fact]
    public async Task Extra_person_can_be_changed_to_a_child_and_removed()
    {
        App.LogInAs(S.AnnaUser);

        await App.Households.SetExtraPersonAsync(S.EventId, "Lille Ida", ParticipantType.Child);
        (await App.Events.GetAsync(S.EventId)).ExtraPerson.ShouldNotBeNull().Weight.ShouldBe(0.5m);

        await App.Households.RemoveExtraPersonAsync(S.EventId);
        (await App.Events.GetAsync(S.EventId)).ExtraPerson.ShouldBeNull();
    }

    [Fact]
    public async Task Family_can_maintain_its_own_participants()
    {
        App.LogInAs(S.BoUser);

        var id = await App.Households.AddParticipantAsync(S.EventId, S.B, "Baby", ParticipantType.Child);
        await App.Households.UpdateParticipantAsync(S.EventId, id, "Birk", ParticipantType.Child);
        await App.Households.RenameHouseholdAsync(S.EventId, S.B, "Familien Berg");

        var details = await App.Events.GetAsync(S.EventId);
        var b = details.Households.Single(h => h.Id == S.B);
        b.Name.ShouldBe("Familien Berg");
        b.Participants.Select(p => p.Name).ShouldContain("Birk");
        b.Weight.ShouldBe(3.0m);

        await App.Households.RemoveParticipantAsync(S.EventId, id);
        (await App.Events.GetAsync(S.EventId)).Households.Single(h => h.Id == S.B).Weight.ShouldBe(2.5m);
    }
}
