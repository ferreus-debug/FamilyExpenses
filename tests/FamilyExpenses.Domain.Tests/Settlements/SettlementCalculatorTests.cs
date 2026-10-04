using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;
using FamilyExpenses.Domain.Settlements;
using static FamilyExpenses.Domain.Tests.TestData;

namespace FamilyExpenses.Domain.Tests.Settlements;

public sealed class SettlementCalculatorTests
{
    [Fact]
    public void Plan_example_gives_expected_balances_and_three_transfers()
    {
        var ex = CreatePlanExample();
        ex.Event.AddExpense("Sommerhus", Kr(4250), Today, ex.Anna.Id, null, UserId);
        ex.Event.AddExpense("Indkøb", Kr(1700), Today, ex.Bo.Id, null, UserId);
        ex.Event.AddExpense("Restaurant", Kr(2550), Today, ex.Carla.Id, null, UserId);

        var settlement = ex.Event.CalculateSettlement();

        settlement.Total.ShouldBe(Kr(8500));
        ShouldHaveBalance(settlement, ex.A, weight: 3.0m, paid: 4250, share: 3000, balance: 1250);
        ShouldHaveBalance(settlement, ex.B, weight: 2.5m, paid: 1700, share: 2500, balance: -800);
        ShouldHaveBalance(settlement, ex.C, weight: 2.0m, paid: 2550, share: 2000, balance: 550);
        ShouldHaveBalance(settlement, ex.X, weight: 1.0m, paid: 0, share: 1000, balance: -1000);

        settlement.Transfers.ShouldBe(
        [
            new Transfer(ex.X.Id, "Xenia", ex.A.Id, "Familie A", Kr(1000)),
            new Transfer(ex.B.Id, "Familie B", ex.C.Id, "Familie C", Kr(550)),
            new Transfer(ex.B.Id, "Familie B", ex.A.Id, "Familie A", Kr(250)),
        ]);
    }

    [Fact]
    public void Only_adults_split_equally()
    {
        var expenseEvent = new ExpenseEvent("Weekend");
        var a = expenseEvent.AddFamily("A");
        var b = expenseEvent.AddFamily("B");
        var payer = expenseEvent.AddParticipant(a.Id, "Ane", ParticipantType.Adult);
        expenseEvent.AddParticipant(b.Id, "Bent", ParticipantType.Adult);
        expenseEvent.AddExpense("Mad", Kr(500), Today, payer.Id, null, UserId);

        var settlement = expenseEvent.CalculateSettlement();

        Balance(settlement, a).Share.ShouldBe(Kr(250));
        Balance(settlement, b).Share.ShouldBe(Kr(250));
        settlement.Transfers.ShouldHaveSingleItem().ShouldBe(new Transfer(b.Id, "B", a.Id, "A", Kr(250)));
    }

    [Fact]
    public void Baby_takes_part_but_counts_nothing()
    {
        var expenseEvent = new ExpenseEvent("Weekend");
        var a = expenseEvent.AddFamily("A");
        var b = expenseEvent.AddFamily("B");
        var c = expenseEvent.AddFamily("C");
        var payer = expenseEvent.AddParticipant(a.Id, "Ane", ParticipantType.Adult);
        expenseEvent.AddParticipant(a.Id, "Lille Alf", ParticipantType.Baby);
        expenseEvent.AddParticipant(b.Id, "Bent", ParticipantType.Adult);
        var baby = expenseEvent.AddParticipant(c.Id, "Lille Cille", ParticipantType.Baby);
        expenseEvent.AddExpense("Mad", Kr(500), Today, payer.Id, null, UserId);
        expenseEvent.AddExpense("Bleer", Kr(90), Today, baby.Id, [payer.Id], UserId);

        var settlement = expenseEvent.CalculateSettlement();

        ShouldHaveBalance(settlement, a, weight: 1.0m, paid: 500, share: 340, balance: 160);
        ShouldHaveBalance(settlement, b, weight: 1.0m, paid: 0, share: 250, balance: -250);
        ShouldHaveBalance(settlement, c, weight: 0m, paid: 90, share: 0, balance: 90);
    }

    [Fact]
    public void Payments_count_towards_the_balances_and_shrink_the_transfers()
    {
        var ex = CreatePlanExample();
        ex.Event.AddExpense("Sommerhus", Kr(4250), Today, ex.Anna.Id, null, UserId);
        ex.Event.AddExpense("Indkøb", Kr(1700), Today, ex.Bo.Id, null, UserId);
        ex.Event.AddExpense("Restaurant", Kr(2550), Today, ex.Carla.Id, null, UserId);
        Settle(ex.Event);
        ex.Event.RecordPayment(ex.B.Id, ex.C.Id, Kr(550), Today, UserId, DateTimeOffset.UnixEpoch);

        var settlement = ex.Event.CalculateSettlement();

        Balance(settlement, ex.B).Transferred.ShouldBe(Kr(550));
        Balance(settlement, ex.B).Balance.ShouldBe(Kr(-250));
        Balance(settlement, ex.C).Transferred.ShouldBe(Kr(-550));
        Balance(settlement, ex.C).Balance.ShouldBe(Money.Zero);
        settlement.Transfers.ShouldBe(
        [
            new Transfer(ex.X.Id, "Xenia", ex.A.Id, "Familie A", Kr(1000)),
            new Transfer(ex.B.Id, "Familie B", ex.A.Id, "Familie A", Kr(250)),
        ]);

        ex.Event.RecordPayment(ex.X.Id, ex.A.Id, Kr(1000), Today, UserId, DateTimeOffset.UnixEpoch);
        ex.Event.RecordPayment(ex.B.Id, ex.A.Id, Kr(250), Today, UserId, DateTimeOffset.UnixEpoch);
        ex.Event.CalculateSettlement().Transfers.ShouldBeEmpty();
    }

    [Fact]
    public void Breakdown_per_household_adds_up_to_paid_and_share()
    {
        var ex = CreatePlanExample();
        var sommerhus = ex.Event.AddExpense("Sommerhus", Kr(4250), Today, ex.Anna.Id, null, UserId);
        var vin = ex.Event.AddExpense("Vin", Kr(300), Today.AddDays(1), ex.Bo.Id, [ex.Carla.Id, ex.X.Participants[0].Id], UserId);

        var settlement = ex.Event.CalculateSettlement();

        Balance(settlement, ex.A).Expenses.ShouldBe(
            [new ExpenseShare(sommerhus.Id, "Sommerhus", Today, Kr(4250), Kr(1500), new Weight(3.0m), new Weight(8.5m))]);
        Balance(settlement, ex.B).Expenses.ShouldBe(
        [
            new ExpenseShare(sommerhus.Id, "Sommerhus", Today, Money.Zero, Kr(1250), new Weight(2.5m), new Weight(8.5m)),
            new ExpenseShare(vin.Id, "Vin", Today.AddDays(1), Kr(300), Money.Zero, null, new Weight(2.0m)),
        ]);
        Balance(settlement, ex.X).Expenses.Select(e => e.Share).ShouldBe([Kr(500), Kr(150)]);

        foreach (var balance in settlement.Balances)
        {
            balance.Expenses.Aggregate(Money.Zero, (sum, e) => sum + e.Paid).ShouldBe(balance.Paid);
            balance.Expenses.Aggregate(Money.Zero, (sum, e) => sum + e.Share).ShouldBe(balance.Share);
        }
    }

    [Fact]
    public void Extra_person_as_child_counts_half()
    {
        var ex = CreatePlanExample();
        ex.Event.SetExtraPerson("Xenia", ParticipantType.Child);
        ex.Event.AddExpense("Mad", Kr(800), Today, ex.Anna.Id, null, UserId);

        var settlement = ex.Event.CalculateSettlement();

        // Total weight 8.0 → 100 kr. per unit.
        Balance(settlement, ex.A).Share.ShouldBe(Kr(300));
        Balance(settlement, ex.B).Share.ShouldBe(Kr(250));
        Balance(settlement, ex.C).Share.ShouldBe(Kr(200));
        Balance(settlement, ex.X).Share.ShouldBe(Kr(50));
    }

    [Fact]
    public void Subset_expense_is_only_split_between_those_sharing_it()
    {
        var ex = CreatePlanExample();
        ex.Event.AddExpense("Restaurant", Kr(300), Today, ex.Anna.Id, [ex.Bo.Id, ex.Carla.Id], UserId);

        var settlement = ex.Event.CalculateSettlement();

        // The payer does not take part but is credited in full.
        ShouldHaveBalance(settlement, ex.A, weight: 3.0m, paid: 300, share: 0, balance: 300);
        ShouldHaveBalance(settlement, ex.B, weight: 2.5m, paid: 0, share: 150, balance: -150);
        ShouldHaveBalance(settlement, ex.C, weight: 2.0m, paid: 0, share: 150, balance: -150);
        Balance(settlement, ex.X).Share.ShouldBe(Money.Zero);
        settlement.Transfers.Count.ShouldBe(2);
    }

    [Fact]
    public void Payer_is_credited_to_their_own_household()
    {
        var ex = CreatePlanExample();
        var xenia = ex.X.Participants[0];
        ex.Event.AddExpense("Is", Kr(85), Today, xenia.Id, null, UserId);

        var settlement = ex.Event.CalculateSettlement();

        Balance(settlement, ex.X).Paid.ShouldBe(Kr(85));
        Balance(settlement, ex.A).Paid.ShouldBe(Money.Zero);
    }

    [Fact]
    public void Leftover_ore_are_distributed_so_the_total_adds_up()
    {
        var expenseEvent = new ExpenseEvent("Weekend");
        var households = new[] { "A", "B", "C" }.Select(expenseEvent.AddFamily).ToList();
        var people = households.Select(h => expenseEvent.AddParticipant(h.Id, h.Name + "1", ParticipantType.Adult)).ToList();
        expenseEvent.AddExpense("Mad", Kr(100), Today, people[0].Id, null, UserId);

        var settlement = expenseEvent.CalculateSettlement();

        settlement.Balances.Select(b => b.Share).ShouldBe([Kr(33.34m), Kr(33.33m), Kr(33.33m)]);
        settlement.Balances.Aggregate(Money.Zero, (sum, b) => sum + b.Share).ShouldBe(Kr(100));
    }

    [Fact]
    public void No_transfers_when_everyone_paid_their_share()
    {
        var ex = CreatePlanExample();
        ex.Event.AddExpense("A", Kr(300), Today, ex.Anna.Id, [ex.Anna.Id], UserId);
        ex.Event.AddExpense("B", Kr(200), Today, ex.Bo.Id, [ex.Bo.Id], UserId);

        ex.Event.CalculateSettlement().Transfers.ShouldBeEmpty();
    }

    [Fact]
    public void Event_without_expenses_settles_to_zero()
    {
        var settlement = CreatePlanExample().Event.CalculateSettlement();

        settlement.Total.ShouldBe(Money.Zero);
        settlement.Balances.ShouldAllBe(b => b.Balance == Money.Zero);
        settlement.Transfers.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(42)]
    [InlineData(2026)]
    public void Random_scenarios_always_balance_out(int seed)
    {
        var random = new Random(seed);
        var ex = CreatePlanExample();
        var people = ex.Event.Participants.ToList();

        for (var i = 0; i < 25; i++)
        {
            var payer = people[random.Next(people.Count)];
            var sharedWith = people.Where(_ => random.Next(3) > 0).Select(p => p.Id).ToList();
            if (sharedWith.Count == 0)
            {
                sharedWith.Add(payer.Id);
            }

            var amount = Money.FromMinorUnits(random.Next(1, 500_000));
            ex.Event.AddExpense($"Udgift {i}", amount, Today, payer.Id, sharedWith, UserId);
        }

        var settlement = ex.Event.CalculateSettlement();
        var total = ex.Event.Expenses.Aggregate(Money.Zero, (sum, e) => sum + e.Amount);

        settlement.Balances.Aggregate(Money.Zero, (sum, b) => sum + b.Share).ShouldBe(total);
        settlement.Balances.Aggregate(Money.Zero, (sum, b) => sum + b.Balance).ShouldBe(Money.Zero);
        settlement.Transfers.Count.ShouldBeLessThanOrEqualTo(settlement.Balances.Count - 1);
        settlement.Transfers.ShouldAllBe(t => t.Amount > Money.Zero);

        // Applying the transfers must bring every household to exactly zero.
        var remaining = settlement.Balances.ToDictionary(b => b.HouseholdId, b => b.Balance);
        foreach (var transfer in settlement.Transfers)
        {
            remaining[transfer.FromHouseholdId] += transfer.Amount;
            remaining[transfer.ToHouseholdId] -= transfer.Amount;
        }

        remaining.Values.ShouldAllBe(balance => balance == Money.Zero);
    }

    [Fact]
    public void Debts_are_simplified_to_the_fewest_transfers()
    {
        // Balances A -600, B -400, C +400, D +300, E +300. Matching largest with largest would need
        // 4 transfers (A→C, B→D, A→E, B→E); B↔C and A↔(D, E) settle separately with 3.
        var trip = new ExpenseEvent("Roadtrip");
        var (a, b, c, d, e) = (Friend(trip, "A"), Friend(trip, "B"), Friend(trip, "C"), Friend(trip, "D"), Friend(trip, "E"));
        trip.AddExpense("Færge", Kr(400), Today, c.Id, [b.Id], UserId);
        trip.AddExpense("Hotel", Kr(300), Today, d.Id, [a.Id], UserId);
        trip.AddExpense("Middag", Kr(300), Today, e.Id, [a.Id], UserId);

        var transfers = trip.CalculateSettlement().Transfers;

        transfers.Select(t => (t.FromName, t.ToName, t.Amount)).ShouldBe(
        [
            ("B", "C", Kr(400)),
            ("A", "D", Kr(300)),
            ("A", "E", Kr(300)),
        ], ignoreOrder: true);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(2026)]
    public void Trip_with_many_friends_uses_the_minimum_number_of_transfers(int seed)
    {
        // Four separate groups of three: one friend owes the two others. Each group needs exactly two
        // transfers and no fewer are possible, so the minimum for the trip is 8.
        var random = new Random(seed);
        var trip = new ExpenseEvent("Interrail");
        for (var group = 0; group < 4; group++)
        {
            var owes = Friend(trip, $"Skylder {group}");
            for (var creditor = 0; creditor < 2; creditor++)
            {
                var payer = Friend(trip, $"Lægger ud {group}.{creditor}");
                trip.AddExpense($"Udgift {group}.{creditor}", Money.FromMinorUnits(random.Next(1, 500_000)), Today, payer.Id, [owes.Id], UserId);
            }
        }

        var settlement = trip.CalculateSettlement();

        settlement.Transfers.Count.ShouldBe(8);
        var remaining = settlement.Balances.ToDictionary(b => b.HouseholdId, b => b.Balance);
        foreach (var transfer in settlement.Transfers)
        {
            remaining[transfer.FromHouseholdId] += transfer.Amount;
            remaining[transfer.ToHouseholdId] -= transfer.Amount;
        }

        remaining.Values.ShouldAllBe(balance => balance == Money.Zero);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(99)]
    public void Largest_trip_still_balances_out(int seed)
    {
        var random = new Random(seed);
        var trip = new ExpenseEvent("Stor tur");
        for (var i = 0; i < ExpenseEvent.MaxFamilies; i++)
        {
            var family = trip.AddFamily($"Familie {i}");
            for (var p = 0; p <= random.Next(3); p++)
            {
                trip.AddParticipant(family.Id, $"Person {i}.{p}", random.Next(4) == 0 ? ParticipantType.Child : ParticipantType.Adult);
            }
        }

        trip.SetExtraPerson("Ekstra", ParticipantType.Adult);
        var people = trip.Participants.ToList();
        for (var i = 0; i < 40; i++)
        {
            var sharedWith = people.Where(_ => random.Next(2) == 0).Select(p => p.Id).ToList();
            var payer = people[random.Next(people.Count)];
            trip.AddExpense($"Udgift {i}", Money.FromMinorUnits(random.Next(1, 500_000)), Today, payer.Id,
                sharedWith.Count > 0 ? sharedWith : [payer.Id], UserId);
        }

        var settlement = trip.CalculateSettlement();

        settlement.Transfers.Count.ShouldBeLessThanOrEqualTo(settlement.Balances.Count(b => b.Balance != Money.Zero) - 1);
        var remaining = settlement.Balances.ToDictionary(b => b.HouseholdId, b => b.Balance);
        foreach (var transfer in settlement.Transfers)
        {
            remaining[transfer.FromHouseholdId] += transfer.Amount;
            remaining[transfer.ToHouseholdId] -= transfer.Amount;
        }

        remaining.Values.ShouldAllBe(balance => balance == Money.Zero);
    }

    // A friend travelling alone: a household with one adult.
    private static Participant Friend(ExpenseEvent trip, string name) =>
        trip.AddParticipant(trip.AddFamily(name).Id, name, ParticipantType.Adult);

    private static HouseholdBalance Balance(Settlement settlement, Household household) =>
        settlement.Balances.Single(b => b.HouseholdId == household.Id);

    private static void ShouldHaveBalance(
        Settlement settlement,
        Household household,
        decimal weight,
        decimal paid,
        decimal share,
        decimal balance)
    {
        var actual = Balance(settlement, household);
        actual.Weight.Value.ShouldBe(weight);
        actual.Paid.ShouldBe(Kr(paid));
        actual.Share.ShouldBe(Kr(share));
        actual.Balance.ShouldBe(Kr(balance));
    }
}
