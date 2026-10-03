using FamilyExpenses.Application.Common;
using FamilyExpenses.Application.Invitations;
using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Application.Tests;

public sealed class InvitationServiceTests : ServiceTestBase
{
    [Fact]
    public async Task Preview_shows_event_and_household_without_login()
    {
        App.LogInAs(S.AnnaUser);
        var invitation = await App.Invitations.CreateAsync(S.EventId, S.X);

        App.LogInAs(null);
        var preview = await App.Invitations.PreviewAsync(invitation.Token);

        preview.ShouldBe(new InvitationPreview("Sommerhus 2026", "Xenia", IsValid: true));
        (await App.Invitations.PreviewAsync("ukendt-token")).ShouldBeNull();
    }

    [Fact]
    public async Task Accepting_joins_the_user_to_the_household()
    {
        var xenia = await App.Db.AddUserAsync("Xenia");
        App.LogInAs(S.AnnaUser);
        var invitation = await App.Invitations.CreateAsync(S.EventId, S.X);

        App.LogInAs(xenia);
        (await App.Invitations.AcceptAsync(invitation.Token)).ShouldBe(S.EventId);

        var details = await App.Events.GetAsync(S.EventId);
        details.MyHouseholdId.ShouldBe(S.X);
        details.IsAdmin.ShouldBeFalse();
    }

    [Fact]
    public async Task Link_can_only_be_used_once()
    {
        var eve = await App.Db.AddUserAsync("Eve");
        App.LogInAs(S.AnnaUser);
        var invitation = await App.Invitations.CreateAsync(S.EventId, S.X);
        App.LogInAs(S.DorteUser);
        await App.Invitations.AcceptAsync(invitation.Token);

        App.LogInAs(eve);
        var error = await Should.ThrowAsync<DomainException>(() => App.Invitations.AcceptAsync(invitation.Token));
        error.Message.ShouldBe("Invitationen er allerede brugt.");
        (await App.Invitations.PreviewAsync(invitation.Token)).ShouldNotBeNull().IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Existing_member_cannot_accept_another_invitation_to_the_same_event()
    {
        App.LogInAs(S.AnnaUser);
        var invitation = await App.Invitations.CreateAsync(S.EventId, S.C);

        App.LogInAs(S.BoUser);
        var error = await Should.ThrowAsync<DomainException>(() => App.Invitations.AcceptAsync(invitation.Token));
        error.Message.ShouldBe("Du er allerede med i denne begivenhed.");
    }

    [Fact]
    public async Task Link_expires_after_two_weeks()
    {
        App.LogInAs(S.AnnaUser);
        var invitation = await App.Invitations.CreateAsync(S.EventId, S.X);

        App.Time.Advance(TimeSpan.FromDays(14));

        App.LogInAs(S.DorteUser);
        (await App.Invitations.PreviewAsync(invitation.Token)).ShouldNotBeNull().IsValid.ShouldBeFalse();
        (await Should.ThrowAsync<DomainException>(() => App.Invitations.AcceptAsync(invitation.Token)))
            .Message.ShouldBe("Invitationen er udløbet.");
    }

    [Fact]
    public async Task Admin_sees_status_of_all_invitations()
    {
        App.LogInAs(S.AnnaUser);
        await App.Invitations.CreateAsync(S.EventId, S.X);
        App.Time.Advance(TimeSpan.FromDays(20));
        var fresh = await App.Invitations.CreateAsync(S.EventId, S.X);

        var list = await App.Invitations.ListAsync(S.EventId);

        list.Count.ShouldBe(4);
        list.Count(i => i.Status == InvitationStatus.Accepted).ShouldBe(2);
        list.Where(i => i.Status == InvitationStatus.Accepted).Select(i => i.AcceptedByName).ShouldBe(["Bo", "Carla"], ignoreOrder: true);
        list.Single(i => i.Id == fresh.Id).Status.ShouldBe(InvitationStatus.Pending);
        list.Count(i => i.Status == InvitationStatus.Expired).ShouldBe(1);
        list.ShouldAllBe(i => i.HouseholdName.Length > 0);
    }

    [Fact]
    public async Task Cannot_invite_to_a_household_outside_the_event()
    {
        App.LogInAs(S.AnnaUser);

        await Should.ThrowAsync<DomainException>(() => App.Invitations.CreateAsync(S.EventId, Guid.NewGuid()));
    }

    [Fact]
    public async Task Accepting_requires_login()
    {
        App.LogInAs(S.AnnaUser);
        var invitation = await App.Invitations.CreateAsync(S.EventId, S.X);

        App.LogInAs(null);
        await Should.ThrowAsync<ForbiddenException>(() => App.Invitations.AcceptAsync(invitation.Token));
    }
}
