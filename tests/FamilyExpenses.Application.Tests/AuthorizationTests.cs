using FamilyExpenses.Application.Common;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Application.Tests;

/// <summary>Access rules are enforced by the services, not only by the UI.</summary>
public sealed class AuthorizationTests : ServiceTestBase
{
    [Fact]
    public async Task Anonymous_user_is_rejected()
    {
        App.LogInAs(null);

        await Should.ThrowAsync<ForbiddenException>(() => App.Events.ListMineAsync());
        await Should.ThrowAsync<ForbiddenException>(() => App.Events.GetAsync(S.EventId));
        await Should.ThrowAsync<ForbiddenException>(() => App.Events.CreateAsync("Hack"));
    }

    [Fact]
    public async Task Stranger_cannot_see_that_the_event_exists()
    {
        App.LogInAs(S.DorteUser);

        await Should.ThrowAsync<NotFoundException>(() => App.Events.GetAsync(S.EventId));
        await Should.ThrowAsync<NotFoundException>(() => App.Expenses.ListAsync(S.EventId));
        await Should.ThrowAsync<NotFoundException>(() => App.Settlements.GetAsync(S.EventId));
        await Should.ThrowAsync<NotFoundException>(
            () => App.Expenses.AddAsync(S.EventId, new("Hack", 1, AppHarness.Today, S.Person("Anna"), null)));
        await Should.ThrowAsync<NotFoundException>(() => App.Events.DeleteAsync(S.EventId));
    }

    [Fact]
    public async Task Members_cannot_manage_the_event_structure()
    {
        App.LogInAs(S.BoUser);

        await Should.ThrowAsync<ForbiddenException>(() => App.Events.RenameAsync(S.EventId, "Mit"));
        await Should.ThrowAsync<ForbiddenException>(() => App.Events.CloseAsync(S.EventId));
        await Should.ThrowAsync<ForbiddenException>(() => App.Events.ReopenAsync(S.EventId));
        await Should.ThrowAsync<ForbiddenException>(() => App.Events.DeleteAsync(S.EventId));
        await Should.ThrowAsync<ForbiddenException>(() => App.Events.JoinHouseholdAsync(S.EventId, S.C));
        await Should.ThrowAsync<ForbiddenException>(() => App.Households.AddFamilyAsync(S.EventId, "D"));
        await Should.ThrowAsync<ForbiddenException>(() => App.Households.RemoveFamilyAsync(S.EventId, S.C));
        await Should.ThrowAsync<ForbiddenException>(
            () => App.Households.SetExtraPersonAsync(S.EventId, "Ny", ParticipantType.Adult));
        await Should.ThrowAsync<ForbiddenException>(() => App.Households.RemoveExtraPersonAsync(S.EventId));
        await Should.ThrowAsync<ForbiddenException>(() => App.Invitations.CreateAsync(S.EventId, S.B));
        await Should.ThrowAsync<ForbiddenException>(() => App.Invitations.ListAsync(S.EventId));
    }

    [Fact]
    public async Task Members_cannot_change_another_familys_participants()
    {
        App.LogInAs(S.BoUser);

        await Should.ThrowAsync<ForbiddenException>(
            () => App.Households.AddParticipantAsync(S.EventId, S.C, "Snyd", ParticipantType.Adult));
        await Should.ThrowAsync<ForbiddenException>(
            () => App.Households.UpdateParticipantAsync(S.EventId, S.Person("Carla"), "Snyd", ParticipantType.Child));
        await Should.ThrowAsync<ForbiddenException>(
            () => App.Households.RemoveParticipantAsync(S.EventId, S.Person("Cilius")));
        await Should.ThrowAsync<ForbiddenException>(
            () => App.Households.RenameHouseholdAsync(S.EventId, S.C, "Snyd"));
        await Should.ThrowAsync<ForbiddenException>(
            () => App.Households.UpdateParticipantAsync(S.EventId, S.Person("Xenia"), "Snyd", ParticipantType.Child));
    }

    [Fact]
    public async Task Admin_can_change_any_familys_participants()
    {
        App.LogInAs(S.AnnaUser);

        await App.Households.UpdateParticipantAsync(S.EventId, S.Person("Carla"), "Carla C.", ParticipantType.Adult);

        (await App.Events.GetAsync(S.EventId)).Households.Single(h => h.Id == S.C)
            .Participants[0].Name.ShouldBe("Carla C.");
    }
}
