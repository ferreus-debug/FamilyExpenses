using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Domain.Tests;

internal static class TestData
{
    public const string UserId = "user-1";

    public static readonly DateOnly Today = new(2026, 7, 1);

    public static Money Kr(decimal amount) => new(amount);

    /// <summary>The worked example from docs/PLAN.md section 6.</summary>
    public static PlanExample CreatePlanExample()
    {
        var expenseEvent = new ExpenseEvent("Sommerhus 2026");

        var a = expenseEvent.AddFamily("Familie A");
        var anna = expenseEvent.AddParticipant(a.Id, "Anna", ParticipantType.Adult);
        expenseEvent.AddParticipant(a.Id, "Anders", ParticipantType.Adult);
        expenseEvent.AddParticipant(a.Id, "Alma", ParticipantType.Child);
        expenseEvent.AddParticipant(a.Id, "Arne", ParticipantType.Child);

        var b = expenseEvent.AddFamily("Familie B");
        var bo = expenseEvent.AddParticipant(b.Id, "Bo", ParticipantType.Adult);
        expenseEvent.AddParticipant(b.Id, "Bente", ParticipantType.Adult);
        expenseEvent.AddParticipant(b.Id, "Bjørn", ParticipantType.Child);

        var c = expenseEvent.AddFamily("Familie C");
        var carla = expenseEvent.AddParticipant(c.Id, "Carla", ParticipantType.Adult);
        expenseEvent.AddParticipant(c.Id, "Cilius", ParticipantType.Child);
        expenseEvent.AddParticipant(c.Id, "Cecilie", ParticipantType.Child);

        var x = expenseEvent.SetExtraPerson("Xenia", ParticipantType.Adult);

        return new PlanExample(expenseEvent, a, b, c, x, anna, bo, carla);
    }

    /// <summary>Closes the event and lets every household approve, so the amounts are final.</summary>
    public static void Settle(ExpenseEvent expenseEvent)
    {
        expenseEvent.Close();
        foreach (var household in expenseEvent.HouseholdsToApprove.ToList())
        {
            expenseEvent.Approve(household.Id, UserId, DateTimeOffset.UnixEpoch);
        }
    }
}

internal sealed record PlanExample(
    ExpenseEvent Event,
    Household A,
    Household B,
    Household C,
    Household X,
    Participant Anna,
    Participant Bo,
    Participant Carla);
