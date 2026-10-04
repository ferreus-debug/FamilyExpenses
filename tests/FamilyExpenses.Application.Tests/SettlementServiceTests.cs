using FamilyExpenses.Application.Common;
using FamilyExpenses.Application.Settlements;
using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Application.Tests;

public sealed class SettlementServiceTests : ServiceTestBase
{
    [Fact]
    public async Task Plan_example_end_to_end_through_the_services()
    {
        App.LogInAs(S.AnnaUser);
        await App.Expenses.AddAsync(S.EventId, new("Sommerhus", 4250, AppHarness.Today, S.Person("Anna"), null));
        App.LogInAs(S.BoUser);
        await App.Expenses.AddAsync(S.EventId, new("Indkøb", 1200, AppHarness.Today, S.Person("Bo"), null));
        await App.Expenses.AddAsync(S.EventId, new("Benzin", 500, AppHarness.Today, S.Person("Bente"), null));
        App.LogInAs(S.CarlaUser);
        await App.Expenses.AddAsync(S.EventId, new("Restaurant", 2550, AppHarness.Today, S.Person("Carla"), null));

        var settlement = await App.Settlements.GetAsync(S.EventId);

        settlement.Total.ShouldBe(8500m);
        settlement.TotalWeight.ShouldBe(8.5m);
        settlement.Balances.Select(b => (b.Name, b.Weight, b.Paid, b.Share, b.Balance)).ShouldBe(
        [
            ("Familie A", 3.0m, 4250m, 3000m, 1250m),
            ("Familie B", 2.5m, 1700m, 2500m, -800m),
            ("Familie C", 2.0m, 2550m, 2000m, 550m),
            ("Xenia", 1.0m, 0m, 1000m, -1000m),
        ]);
        settlement.Balances.Single(b => b.HouseholdId == S.B).PaidByPerson.ShouldBe(
        [
            new PersonPaidDto(S.Person("Bo"), "Bo", 1200m),
            new PersonPaidDto(S.Person("Bente"), "Bente", 500m),
        ]);
        settlement.Transfers.Select(t => (t.FromName, t.ToName, t.Amount)).ShouldBe(
        [
            ("Xenia", "Familie A", 1000m),
            ("Familie B", "Familie C", 550m),
            ("Familie B", "Familie A", 250m),
        ]);
    }

    [Fact]
    public async Task Paying_a_transfer_removes_it_and_can_be_undone()
    {
        await AddPlanExpensesAsync();
        await SettleAsync();

        App.LogInAs(S.BoUser);
        var before = await App.Settlements.GetAsync(S.EventId);
        before.Transfers.Select(t => t.CanMarkPaid).ShouldBe([false, true, true]);
        var paymentId = await App.Settlements.RecordPaymentAsync(S.EventId, S.B, S.C, 550m);

        var after = await App.Settlements.GetAsync(S.EventId);
        after.Transfers.Select(t => (t.FromName, t.ToName, t.Amount)).ShouldBe(
        [
            ("Xenia", "Familie A", 1000m),
            ("Familie B", "Familie A", 250m),
        ]);
        after.Balances.Single(b => b.HouseholdId == S.B).Transferred.ShouldBe(550m);
        after.Payments.ShouldHaveSingleItem().ShouldBe(
            new PaymentDto(paymentId, S.B, "Familie B", S.C, "Familie C", 550m, AppHarness.Today, "Bo", CanRemove: true));

        await App.Settlements.RemovePaymentAsync(S.EventId, paymentId);
        (await App.Settlements.GetAsync(S.EventId)).Transfers.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Only_the_two_households_or_the_admin_handle_a_payment()
    {
        await AddPlanExpensesAsync();
        await SettleAsync();

        App.LogInAs(S.CarlaUser);
        await Should.ThrowAsync<ForbiddenException>(() => App.Settlements.RecordPaymentAsync(S.EventId, S.B, S.A, 250m));
        var paymentId = await App.Settlements.RecordPaymentAsync(S.EventId, S.B, S.C, 550m);

        App.LogInAs(S.AnnaUser);
        (await App.Settlements.GetAsync(S.EventId)).Payments.ShouldHaveSingleItem().CanRemove.ShouldBeTrue();
        App.Time.Advance(TimeSpan.FromMinutes(1));
        await App.Settlements.RecordPaymentAsync(S.EventId, S.X, S.A, 1000m);

        App.LogInAs(S.BoUser);
        var payments = (await App.Settlements.GetAsync(S.EventId)).Payments;
        payments.Select(p => (p.FromName, p.CanRemove)).ShouldBe([("Xenia", false), ("Familie B", true)]);
        await Should.ThrowAsync<ForbiddenException>(() => App.Settlements.RemovePaymentAsync(S.EventId, payments[0].Id));
        await App.Settlements.RemovePaymentAsync(S.EventId, paymentId);

        App.LogInAs(S.DorteUser);
        await Should.ThrowAsync<NotFoundException>(() => App.Settlements.RecordPaymentAsync(S.EventId, S.B, S.C, 1m));
    }

    [Fact]
    public async Task Breakdown_explains_each_households_share()
    {
        await AddPlanExpensesAsync();

        var b = (await App.Settlements.GetAsync(S.EventId)).Balances.Single(h => h.HouseholdId == S.B);

        b.Expenses.Select(e => (e.Description, e.Paid, e.Share, e.HouseholdWeight, e.SharedWeight)).ShouldBe(
        [
            ("Benzin", 500m, 147.06m, (decimal?)2.5m, 8.5m),
            ("Indkøb", 1200m, 352.94m, 2.5m, 8.5m),
            ("Restaurant", 0m, 750m, 2.5m, 8.5m),
            ("Sommerhus", 0m, 1250m, 2.5m, 8.5m),
        ]);
    }

    [Fact]
    public async Task Event_with_payments_can_be_deleted()
    {
        await AddPlanExpensesAsync();
        await SettleAsync();
        await App.Settlements.RecordPaymentAsync(S.EventId, S.B, S.A, 250m);

        await App.Events.DeleteAsync(S.EventId);

        await Should.ThrowAsync<NotFoundException>(() => App.Settlements.GetAsync(S.EventId));
    }

    [Fact]
    public async Task Amounts_become_final_when_every_family_has_approved()
    {
        await AddPlanExpensesAsync();
        (await App.Settlements.GetAsync(S.EventId)).IsSettled.ShouldBeFalse();
        await Should.ThrowAsync<DomainException>(() => App.Events.ApproveAsync(S.EventId, S.A));

        App.LogInAs(S.BoUser);
        await Should.ThrowAsync<ForbiddenException>(() => App.Events.CloseAsync(S.EventId));
        App.LogInAs(S.AnnaUser);
        await App.Events.CloseAsync(S.EventId);

        App.LogInAs(S.BoUser);
        await Should.ThrowAsync<ForbiddenException>(() => App.Events.ApproveAsync(S.EventId, S.C));
        await App.Events.ApproveAsync(S.EventId, S.B);
        var details = await App.Events.GetAsync(S.EventId);
        details.Approvals.Select(a => (a.Name, a.IsApproved, a.ApprovedByName, a.CanApprove)).ShouldBe(
        [
            ("Familie A", false, null, false),
            ("Familie B", true, "Bo", true),
            ("Familie C", false, null, false),
            ("Xenia", false, null, false),
        ]);
        var preliminary = await App.Settlements.GetAsync(S.EventId);
        preliminary.IsSettled.ShouldBeFalse();
        preliminary.Transfers.ShouldAllBe(t => !t.CanMarkPaid);
        await Should.ThrowAsync<DomainException>(() => App.Settlements.RecordPaymentAsync(S.EventId, S.B, S.C, 550m));

        App.LogInAs(S.CarlaUser);
        await App.Events.ApproveAsync(S.EventId, S.C);
        App.LogInAs(S.AnnaUser);
        await App.Events.ApproveAsync(S.EventId, S.A);
        (await App.Events.GetAsync(S.EventId)).Status.ShouldBe(EventStatus.Closed);

        // Xenia, the extra person, has no login here, so the admin approves on her behalf.
        await App.Events.ApproveAsync(S.EventId, S.X);

        (await App.Events.GetAsync(S.EventId)).Status.ShouldBe(EventStatus.Settled);
        var final = await App.Settlements.GetAsync(S.EventId);
        final.IsSettled.ShouldBeTrue();
        final.Transfers.ShouldAllBe(t => t.CanMarkPaid);
    }

    [Fact]
    public async Task The_extra_person_approves_her_own_part_when_she_has_a_login()
    {
        App.LogInAs(S.AnnaUser);
        var invite = await App.Invitations.CreateAsync(S.EventId, S.X);
        var xenia = await App.Db.AddUserAsync("Xenia");
        App.LogInAs(xenia);
        await App.Invitations.AcceptAsync(invite.Token);

        App.LogInAs(S.AnnaUser);
        await App.Events.CloseAsync(S.EventId);

        App.LogInAs(xenia);
        (await App.Events.GetAsync(S.EventId)).Approvals.Where(a => a.CanApprove).Select(a => a.Name).ShouldBe(["Xenia"]);
        await Should.ThrowAsync<ForbiddenException>(() => App.Events.ApproveAsync(S.EventId, S.A));
        await App.Events.ApproveAsync(S.EventId, S.X);

        (await App.Events.GetAsync(S.EventId)).Approvals.Single(a => a.HouseholdId == S.X).ApprovedByName.ShouldBe("Xenia");
    }

    [Fact]
    public async Task A_family_can_withdraw_its_approval_and_the_admin_can_approve_for_a_family()
    {
        App.LogInAs(S.AnnaUser);
        await App.Events.CloseAsync(S.EventId);

        App.LogInAs(S.BoUser);
        await App.Events.ApproveAsync(S.EventId, S.B);
        await App.Events.WithdrawApprovalAsync(S.EventId, S.B);
        App.LogInAs(S.CarlaUser);
        await Should.ThrowAsync<ForbiddenException>(() => App.Events.WithdrawApprovalAsync(S.EventId, S.B));

        App.LogInAs(S.AnnaUser);
        (await App.Events.GetAsync(S.EventId)).Approvals.ShouldAllBe(a => a.CanApprove && !a.IsApproved);
        await App.Events.ApproveAsync(S.EventId, S.B);
        (await App.Events.GetAsync(S.EventId)).Approvals.Single(a => a.HouseholdId == S.B).ApprovedByName.ShouldBe("Anna");
    }

    /// <summary>Anna closes the trip, each family approves and Anna approves for Xenia (no login).</summary>
    private async Task SettleAsync()
    {
        App.LogInAs(S.AnnaUser);
        await App.Events.CloseAsync(S.EventId);
        await App.Events.ApproveAsync(S.EventId, S.A);
        App.LogInAs(S.BoUser);
        await App.Events.ApproveAsync(S.EventId, S.B);
        App.LogInAs(S.CarlaUser);
        await App.Events.ApproveAsync(S.EventId, S.C);
        App.LogInAs(S.AnnaUser);
        await App.Events.ApproveAsync(S.EventId, S.X);
    }

    private async Task AddPlanExpensesAsync()
    {
        App.LogInAs(S.AnnaUser);
        await App.Expenses.AddAsync(S.EventId, new("Sommerhus", 4250, AppHarness.Today, S.Person("Anna"), null));
        await App.Expenses.AddAsync(S.EventId, new("Indkøb", 1200, AppHarness.Today, S.Person("Bo"), null));
        await App.Expenses.AddAsync(S.EventId, new("Benzin", 500, AppHarness.Today, S.Person("Bente"), null));
        await App.Expenses.AddAsync(S.EventId, new("Restaurant", 2550, AppHarness.Today, S.Person("Carla"), null));
    }
}
