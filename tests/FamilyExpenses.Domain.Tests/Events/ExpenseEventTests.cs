using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;
using static FamilyExpenses.Domain.Tests.TestData;

namespace FamilyExpenses.Domain.Tests.Events;

public sealed class ExpenseEventTests
{
    [Fact]
    public void Allows_at_most_twelve_families()
    {
        var expenseEvent = new ExpenseEvent("Ferie");
        for (var i = 1; i <= ExpenseEvent.MaxFamilies; i++)
        {
            expenseEvent.AddFamily($"Familie {i}");
        }

        expenseEvent.Families.Count().ShouldBe(12);
        Should.Throw<DomainException>(() => expenseEvent.AddFamily("Én for meget"));
    }

    [Fact]
    public void Household_names_must_be_unique_ignoring_case()
    {
        var expenseEvent = new ExpenseEvent("Ferie");
        expenseEvent.AddFamily("Hansen");

        Should.Throw<DomainException>(() => expenseEvent.AddFamily("hansen"));
    }

    [Fact]
    public void Households_and_participants_get_increasing_sort_order()
    {
        var example = CreatePlanExample();

        example.Event.Households.Select(h => h.SortOrder).ShouldBe([0, 1, 2, 3]);
        example.A.Participants.Select(p => p.SortOrder).ShouldBe([0, 1, 2, 3]);

        example.Event.RemoveParticipant(example.Carla.Id);
        example.Event.AddParticipant(example.C.Id, "Ny", ParticipantType.Adult).SortOrder.ShouldBe(3);
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
    public void Expense_must_be_shared_by_someone_who_counts()
    {
        var example = CreatePlanExample();
        var baby = example.Event.AddParticipant(example.C.Id, "Lille Ida", ParticipantType.Baby);

        Should.Throw<DomainException>(() => example.Event.AddExpense("Bleer", Kr(90), Today, example.Carla.Id, [baby.Id], UserId));

        var expense = example.Event.AddExpense("Bleer", Kr(90), Today, example.Carla.Id, [baby.Id, example.Carla.Id], UserId);
        Should.Throw<DomainException>(() => example.Event.UpdateExpense(expense.Id, "Bleer", Kr(90), Today, example.Carla.Id, [baby.Id]));
    }

    [Fact]
    public void Changing_to_baby_is_refused_when_an_expense_would_have_nobody_who_counts()
    {
        var example = CreatePlanExample();
        var baby = example.Event.AddParticipant(example.C.Id, "Lille Ida", ParticipantType.Baby);
        example.Event.AddExpense("Bleer", Kr(90), Today, example.Carla.Id, [baby.Id, example.Carla.Id], UserId);

        Should.Throw<DomainException>(() => example.Event.UpdateParticipant(example.Carla.Id, "Carla", ParticipantType.Baby));
        example.Carla.Type.ShouldBe(ParticipantType.Adult);

        example.Event.UpdateParticipant(example.Bo.Id, "Bo", ParticipantType.Baby);
        example.Bo.Weight.ShouldBe(Weight.Zero);
    }

    [Fact]
    public void Payments_are_recorded_and_removed()
    {
        var example = CreatePlanExample();
        Settle(example.Event);
        var payment = example.Event.RecordPayment(example.B.Id, example.A.Id, Kr(250), Today, UserId, DateTimeOffset.UnixEpoch);

        example.Event.Payments.ShouldBe([payment]);
        example.Event.RemovePayment(payment.Id);
        example.Event.Payments.ShouldBeEmpty();
    }

    [Fact]
    public void Payment_rules_are_enforced()
    {
        var example = CreatePlanExample();
        var expenseEvent = example.Event;
        Settle(expenseEvent);

        Should.Throw<DomainException>(() => expenseEvent.RecordPayment(example.A.Id, example.A.Id, Kr(10), Today, UserId, DateTimeOffset.UnixEpoch));
        Should.Throw<DomainException>(() => expenseEvent.RecordPayment(example.A.Id, example.B.Id, Money.Zero, Today, UserId, DateTimeOffset.UnixEpoch));
        Should.Throw<DomainException>(() => expenseEvent.RecordPayment(example.A.Id, Guid.NewGuid(), Kr(10), Today, UserId, DateTimeOffset.UnixEpoch));
        Should.Throw<DomainException>(() => expenseEvent.RecordPayment(example.A.Id, example.B.Id, Kr(10), Today, " ", DateTimeOffset.UnixEpoch));
        Should.Throw<DomainException>(() => expenseEvent.RemovePayment(Guid.NewGuid()));
    }

    [Fact]
    public void Payments_need_every_family_to_have_approved()
    {
        var example = CreatePlanExample();
        void Pay() => example.Event.RecordPayment(example.B.Id, example.A.Id, Kr(250), Today, UserId, DateTimeOffset.UnixEpoch);

        Should.Throw<DomainException>(Pay);
        example.Event.Close();
        Should.Throw<DomainException>(Pay);

        example.Event.Reopen();
        Settle(example.Event);
        Pay();
        example.Event.Payments.ShouldHaveSingleItem();
    }

    [Fact]
    public void Household_with_payments_cannot_be_removed()
    {
        var expenseEvent = new ExpenseEvent("Weekend");
        var a = expenseEvent.AddFamily("A");
        var b = expenseEvent.AddFamily("B");
        expenseEvent.AddParticipant(a.Id, "Ane", ParticipantType.Adult);
        var x = expenseEvent.SetExtraPerson("Mormor", ParticipantType.Adult);
        Settle(expenseEvent);
        expenseEvent.RecordPayment(a.Id, b.Id, Kr(10), Today, UserId, DateTimeOffset.UnixEpoch);
        expenseEvent.RecordPayment(x.Id, a.Id, Kr(10), Today, UserId, DateTimeOffset.UnixEpoch);
        expenseEvent.Reopen();

        expenseEvent.Payments.Count.ShouldBe(2);
        Should.Throw<DomainException>(() => expenseEvent.RemoveFamily(b.Id));
        Should.Throw<DomainException>(() => expenseEvent.RemoveExtraPerson());
    }

    [Fact]
    public void Closed_event_is_settled_when_the_last_family_approves()
    {
        var example = CreatePlanExample();
        var expenseEvent = example.Event;
        expenseEvent.AddFamily("Tom familie");
        expenseEvent.Close();

        expenseEvent.Status.ShouldBe(EventStatus.Closed);
        expenseEvent.HouseholdsToApprove.ShouldBe([example.A, example.B, example.C, example.X]);
        expenseEvent.Approve(example.A.Id, "anna", DateTimeOffset.UnixEpoch);
        expenseEvent.Approve(example.B.Id, "bo", DateTimeOffset.UnixEpoch);
        expenseEvent.IsSettled.ShouldBeFalse();

        expenseEvent.WithdrawApproval(example.B.Id);
        expenseEvent.IsApproved(example.B.Id).ShouldBeFalse();
        expenseEvent.Approve(example.B.Id, "bo", DateTimeOffset.UnixEpoch);
        expenseEvent.Approve(example.C.Id, "carla", DateTimeOffset.UnixEpoch);
        expenseEvent.IsSettled.ShouldBeFalse();
        expenseEvent.Approve(example.X.Id, "xenia", DateTimeOffset.UnixEpoch);

        expenseEvent.Status.ShouldBe(EventStatus.Settled);
        expenseEvent.Approvals.Select(a => a.ApprovedByUserId).ShouldBe(["anna", "bo", "carla", "xenia"]);
    }

    [Fact]
    public void Approval_rules_are_enforced()
    {
        var example = CreatePlanExample();
        var expenseEvent = example.Event;
        var empty = expenseEvent.AddFamily("Tom familie");
        var approve = (Guid id) => expenseEvent.Approve(id, UserId, DateTimeOffset.UnixEpoch);

        Should.Throw<DomainException>(() => approve(example.A.Id));
        expenseEvent.Close();
        Should.Throw<DomainException>(() => expenseEvent.Close());
        Should.Throw<DomainException>(() => approve(empty.Id));
        Should.Throw<DomainException>(() => approve(Guid.NewGuid()));
        Should.Throw<DomainException>(() => expenseEvent.Approve(example.A.Id, " ", DateTimeOffset.UnixEpoch));
        Should.Throw<DomainException>(() => expenseEvent.WithdrawApproval(example.A.Id));
        approve(example.A.Id);
        Should.Throw<DomainException>(() => approve(example.A.Id));
        approve(example.B.Id);
        approve(example.C.Id);
        approve(example.X.Id);

        Should.Throw<DomainException>(() => expenseEvent.WithdrawApproval(example.A.Id));
        Should.Throw<DomainException>(() => new ExpenseEvent("Tom").Close());
    }

    [Fact]
    public void Reopening_drops_the_approvals()
    {
        var example = CreatePlanExample();
        Settle(example.Event);

        example.Event.Reopen();

        example.Event.Status.ShouldBe(EventStatus.Open);
        example.Event.Approvals.ShouldBeEmpty();
        example.Event.Close();
        example.Event.IsApproved(example.A.Id).ShouldBeFalse();
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
    public void Closed_event_is_locked_until_reopened()
    {
        var example = CreatePlanExample();
        example.Event.Close();

        Should.Throw<DomainException>(() => example.Event.AddExpense("Mad", Kr(10), Today, example.Anna.Id, null, UserId));
        Should.Throw<DomainException>(() => example.Event.AddFamily("D"));
        Should.Throw<DomainException>(() => example.Event.SetExtraPerson("Ny", ParticipantType.Adult));

        example.Event.Reopen();
        example.Event.AddExpense("Mad", Kr(10), Today, example.Anna.Id, null, UserId);
        example.Event.Expenses.Count.ShouldBe(1);
    }
}
