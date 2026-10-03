using FamilyExpenses.Application.Settlements;

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
}
