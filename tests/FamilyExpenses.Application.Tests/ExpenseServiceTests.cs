using FamilyExpenses.Application.Common;
using FamilyExpenses.Application.Expenses;
using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Application.Tests;

public sealed class ExpenseServiceTests : ServiceTestBase
{
    [Fact]
    public async Task Member_registers_expense_paid_by_a_person()
    {
        App.LogInAs(S.BoUser);

        var id = await App.Expenses.AddAsync(
            S.EventId,
            new("Indkøb Netto", 1700, AppHarness.Today, S.Person("Bente"), null));

        var expense = (await App.Expenses.ListAsync(S.EventId)).ShouldHaveSingleItem();
        expense.Id.ShouldBe(id);
        expense.PaidByName.ShouldBe("Bente");
        expense.PaidByHouseholdName.ShouldBe("Familie B");
        expense.SharedWithSummary.ShouldBe("Alle");
        expense.SharedWithParticipantIds.Count.ShouldBe(11);
        expense.CreatedByName.ShouldBe("Bo");
        expense.CanEdit.ShouldBeTrue();
    }

    [Fact]
    public async Task Shared_with_summary_names_whole_families_and_single_people()
    {
        App.LogInAs(S.AnnaUser);
        var familyB = new[] { "Bo", "Bente", "Bjørn" }.Select(S.Person);

        await App.Expenses.AddAsync(
            S.EventId,
            new("Restaurant", 900, AppHarness.Today, S.Person("Anna"), [.. familyB, S.Person("Carla"), S.Person("Xenia")]));

        (await App.Expenses.ListAsync(S.EventId)).ShouldHaveSingleItem().SharedWithSummary.ShouldBe("Familie B, Carla, Xenia");
    }

    [Fact]
    public async Task Only_creator_or_admin_can_change_an_expense()
    {
        App.LogInAs(S.CarlaUser);
        var carlas = await App.Expenses.AddAsync(S.EventId, new("Is", 85, AppHarness.Today, S.Person("Carla"), null));

        App.LogInAs(S.BoUser);
        (await App.Expenses.ListAsync(S.EventId)).ShouldHaveSingleItem().CanEdit.ShouldBeFalse();
        await Should.ThrowAsync<ForbiddenException>(
            () => App.Expenses.UpdateAsync(S.EventId, carlas, new("Snyd", 1, AppHarness.Today, S.Person("Bo"), null)));
        await Should.ThrowAsync<ForbiddenException>(() => App.Expenses.RemoveAsync(S.EventId, carlas));

        App.LogInAs(S.CarlaUser);
        await App.Expenses.UpdateAsync(
            S.EventId,
            carlas,
            new("Is og slik", 95.50m, AppHarness.Today, S.Person("Carla"), [S.Person("Cilius"), S.Person("Cecilie")]));
        var updated = (await App.Expenses.ListAsync(S.EventId)).ShouldHaveSingleItem();
        updated.Amount.ShouldBe(95.50m);
        updated.SharedWithSummary.ShouldBe("Cilius, Cecilie");

        App.LogInAs(S.AnnaUser);
        (await App.Expenses.ListAsync(S.EventId)).ShouldHaveSingleItem().CanEdit.ShouldBeTrue();
        await App.Expenses.RemoveAsync(S.EventId, carlas);
        (await App.Expenses.ListAsync(S.EventId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Invalid_input_is_rejected_with_a_readable_message()
    {
        App.LogInAs(S.BoUser);

        var tooPrecise = await Should.ThrowAsync<DomainException>(
            () => App.Expenses.AddAsync(S.EventId, new("Mad", 10.005m, AppHarness.Today, S.Person("Bo"), null)));
        tooPrecise.Message.ShouldBe("Beløb må højst have to decimaler.");

        await Should.ThrowAsync<DomainException>(
            () => App.Expenses.AddAsync(S.EventId, new("Mad", 0, AppHarness.Today, S.Person("Bo"), null)));
        await Should.ThrowAsync<DomainException>(
            () => App.Expenses.AddAsync(S.EventId, new("Mad", 10, AppHarness.Today, Guid.NewGuid(), null)));
    }

    [Fact]
    public async Task Expenses_are_listed_newest_first()
    {
        App.LogInAs(S.AnnaUser);
        await App.Expenses.AddAsync(S.EventId, new("Første", 10, AppHarness.Today, S.Person("Anna"), null));
        await App.Expenses.AddAsync(S.EventId, new("Sidste", 10, AppHarness.Today.AddDays(3), S.Person("Anna"), null));

        (await App.Expenses.ListAsync(S.EventId)).Select(e => e.Description).ShouldBe(["Sidste", "Første"]);
    }
}
