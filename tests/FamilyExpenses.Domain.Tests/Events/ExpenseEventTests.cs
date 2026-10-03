using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;
using static FamilyExpenses.Domain.Tests.TestData;

namespace FamilyExpenses.Domain.Tests.Events;

public sealed class ExpenseEventTests
{
    [Fact]
    public void Allows_at_most_three_families()
    {
        var expenseEvent = new ExpenseEvent("Ferie");
        expenseEvent.AddFamily("A");
        expenseEvent.AddFamily("B");
        expenseEvent.AddFamily("C");

        Should.Throw<DomainException>(() => expenseEvent.AddFamily("D"));
    }

    [Fact]
    public void Household_names_must_be_unique_ignoring_case()
    {
        var expenseEvent = new ExpenseEvent("Ferie");
        expenseEvent.AddFamily("Hansen");

        Should.Throw<DomainException>(() => expenseEvent.AddFamily("hansen"));
    }

    [Fact]
    public void Failed_rename_leaves_names_unchanged()
    {
        var example = CreatePlanExample();

        Should.Throw<DomainException>(() => example.Event.RenameHousehold(example.B.Id, "Familie A"));
        Should.Throw<DomainException>(() => example.Event.SetExtraPerson("familie c", ParticipantType.Child));

        example.B.Name.ShouldBe("Familie B");
        example.X.Name.ShouldBe("Xenia");
        example.X.Participants[0].Type.ShouldBe(ParticipantType.Adult);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Names_are_required(string name) =>
        Should.Throw<DomainException>(() => new ExpenseEvent("Ferie").AddFamily(name));

    [Fact]
    public void Extra_person_is_its_own_household_with_one_participant()
    {
        var expenseEvent = new ExpenseEvent("Ferie");

        var extra = expenseEvent.SetExtraPerson("Mormor", ParticipantType.Adult);

        extra.Kind.ShouldBe(HouseholdKind.ExtraPerson);
        extra.Participants.ShouldHaveSingleItem().Name.ShouldBe("Mormor");
        expenseEvent.ExtraPerson.ShouldBe(extra);
    }

    [Fact]
    public void Extra_person_can_be_a_child()
    {
        var extra = new ExpenseEvent("Ferie").SetExtraPerson("Nabobarn", ParticipantType.Child);

        extra.TotalWeight.ShouldBe(Weight.Child);
    }

    [Fact]
    public void Setting_extra_person_again_updates_the_same_person()
    {
        var expenseEvent = new ExpenseEvent("Ferie");
        var first = expenseEvent.SetExtraPerson("Mormor", ParticipantType.Adult);
        var participantId = first.Participants[0].Id;

        var second = expenseEvent.SetExtraPerson("Lille Ida", ParticipantType.Child);

        second.Id.ShouldBe(first.Id);
        second.Name.ShouldBe("Lille Ida");
        second.Participants.ShouldHaveSingleItem().Id.ShouldBe(participantId);
        second.TotalWeight.ShouldBe(Weight.Child);
        expenseEvent.Households.Count.ShouldBe(1);
    }

    [Fact]
    public void Participants_cannot_be_added_to_the_extra_person()
    {
        var expenseEvent = new ExpenseEvent("Ferie");
        var extra = expenseEvent.SetExtraPerson("Mormor", ParticipantType.Adult);

        Should.Throw<DomainException>(() => expenseEvent.AddParticipant(extra.Id, "Morfar", ParticipantType.Adult));
    }

    [Fact]
    public void Extra_person_can_be_removed_when_not_used()
    {
        var expenseEvent = new ExpenseEvent("Ferie");
        expenseEvent.SetExtraPerson("Mormor", ParticipantType.Adult);

        expenseEvent.RemoveExtraPerson();

        expenseEvent.ExtraPerson.ShouldBeNull();
    }

    [Fact]
    public void Extra_person_cannot_be_removed_when_part_of_an_expense()
    {
        var example = CreatePlanExample();
        example.Event.AddExpense("Is", Kr(100), Today, example.X.Participants[0].Id, null, UserId);

        Should.Throw<DomainException>(example.Event.RemoveExtraPerson);
    }

    [Fact]
    public void Participant_used_in_an_expense_cannot_be_removed()
    {
        var example = CreatePlanExample();
        example.Event.AddExpense("Mad", Kr(100), Today, example.Anna.Id, [example.Bo.Id], UserId);

        Should.Throw<DomainException>(() => example.Event.RemoveParticipant(example.Anna.Id));
        Should.Throw<DomainException>(() => example.Event.RemoveParticipant(example.Bo.Id));
        Should.Throw<DomainException>(() => example.Event.RemoveFamily(example.A.Id));
    }

    [Fact]
    public void Unused_participant_can_be_removed()
    {
        var example = CreatePlanExample();

        example.Event.RemoveParticipant(example.Carla.Id);

        example.C.Participants.ShouldNotContain(example.Carla);
    }

    [Fact]
    public void Expense_without_explicit_sharing_is_shared_by_everyone_present_now()
    {
        var example = CreatePlanExample();

        var expense = example.Event.AddExpense("Mad", Kr(850), Today, example.Anna.Id, null, UserId);
        example.Event.AddParticipant(example.C.Id, "Kommer senere", ParticipantType.Adult);

        expense.SharedWithParticipantIds.Count.ShouldBe(11);
        expense.PaidByParticipantId.ShouldBe(example.Anna.Id);
        expense.CreatedByUserId.ShouldBe(UserId);
    }

    [Fact]
    public void Expense_can_be_shared_by_a_subset()
    {
        var example = CreatePlanExample();

        var expense = example.Event.AddExpense("Restaurant", Kr(500), Today, example.Bo.Id, [example.Bo.Id, example.Carla.Id], UserId);

        expense.SharedWithParticipantIds.ShouldBe([example.Bo.Id, example.Carla.Id]);
    }

    [Fact]
    public void Expense_rules_are_enforced()
    {
        var example = CreatePlanExample();
        var expenseEvent = example.Event;
        var anna = example.Anna.Id;

        Should.Throw<DomainException>(() => expenseEvent.AddExpense("Mad", Money.Zero, Today, anna, null, UserId));
        Should.Throw<DomainException>(() => expenseEvent.AddExpense("Mad", Kr(-1), Today, anna, null, UserId));
        Should.Throw<DomainException>(() => expenseEvent.AddExpense(" ", Kr(10), Today, anna, null, UserId));
        Should.Throw<DomainException>(() => expenseEvent.AddExpense("Mad", Kr(10), Today, Guid.NewGuid(), null, UserId));
        Should.Throw<DomainException>(() => expenseEvent.AddExpense("Mad", Kr(10), Today, anna, [Guid.NewGuid()], UserId));
        Should.Throw<DomainException>(() => expenseEvent.AddExpense("Mad", Kr(10), Today, anna, [], UserId));
        Should.Throw<DomainException>(() => expenseEvent.AddExpense("Mad", Kr(10), Today, anna, null, ""));
        expenseEvent.Expenses.ShouldBeEmpty();
    }

    [Fact]
    public void Expense_can_be_updated_and_removed()
    {
        var example = CreatePlanExample();
        var expense = example.Event.AddExpense("Mad", Kr(100), Today, example.Anna.Id, null, UserId);

        example.Event.UpdateExpense(expense.Id, "Aftensmad", Kr(120.50m), Today.AddDays(1), example.Bo.Id, [example.Bo.Id]);

        expense.Description.ShouldBe("Aftensmad");
        expense.Amount.ShouldBe(Kr(120.50m));
        expense.Date.ShouldBe(Today.AddDays(1));
        expense.PaidByParticipantId.ShouldBe(example.Bo.Id);
        expense.SharedWithParticipantIds.ShouldBe([example.Bo.Id]);

        example.Event.RemoveExpense(expense.Id);
        example.Event.Expenses.ShouldBeEmpty();
    }

    [Fact]
    public void Settled_event_is_locked_until_reopened()
    {
        var example = CreatePlanExample();
        example.Event.MarkSettled();

        Should.Throw<DomainException>(() => example.Event.AddExpense("Mad", Kr(10), Today, example.Anna.Id, null, UserId));
        Should.Throw<DomainException>(() => example.Event.AddFamily("D"));
        Should.Throw<DomainException>(() => example.Event.SetExtraPerson("Ny", ParticipantType.Adult));

        example.Event.Reopen();
        example.Event.AddExpense("Mad", Kr(10), Today, example.Anna.Id, null, UserId);
        example.Event.Expenses.Count.ShouldBe(1);
    }
}
