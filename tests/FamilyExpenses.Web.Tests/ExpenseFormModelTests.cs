using FamilyExpenses.Application.Events;
using FamilyExpenses.Application.Expenses;
using FamilyExpenses.Domain.Events;
using FamilyExpenses.Web.Components.UI;

namespace FamilyExpenses.Web.Tests;

public sealed class ExpenseFormModelTests
{
    private static readonly DateOnly Today = new(2026, 7, 1);

    private static readonly ParticipantDto Anna = new(Guid.NewGuid(), "Anna", ParticipantType.Adult, 1.0m);
    private static readonly ParticipantDto Alma = new(Guid.NewGuid(), "Alma", ParticipantType.Child, 0.5m);
    private static readonly ParticipantDto Bo = new(Guid.NewGuid(), "Bo", ParticipantType.Adult, 1.0m);

    private static readonly HouseholdDto A = new(Guid.NewGuid(), "Familie A", HouseholdKind.Family, 1.5m, [Anna, Alma], []);
    private static readonly HouseholdDto B = new(Guid.NewGuid(), "Familie B", HouseholdKind.Family, 1.0m, [Bo], []);

    private static ExpenseFormModel NewModel() => new([A, B], Today, Anna.Id);

    [Fact]
    public void Everyone_is_selected_by_default()
    {
        var model = NewModel();

        model.AllSelected.ShouldBeTrue();
        model.SelectedCount.ShouldBe(3);
        model.SelectedWeight.ShouldBe(2.5m);
        model.HouseholdState(A).ShouldBe(true);
        model.PaidByParticipantId.ShouldBe(Anna.Id);
        model.Date.ShouldBe(new DateTime(2026, 7, 1));
    }

    [Fact]
    public void Household_checkbox_is_indeterminate_when_partly_selected()
    {
        var model = NewModel();

        model.SetParticipant(Alma.Id, false);

        model.HouseholdState(A).ShouldBeNull();
        model.AllSelected.ShouldBeFalse();
        model.SelectedWeight.ShouldBe(2.0m);
    }

    [Fact]
    public void Toggling_a_household_selects_all_of_it_unless_already_full()
    {
        var model = NewModel();
        model.SelectAll(false);
        model.SelectedCount.ShouldBe(0);

        model.ToggleHousehold(A);
        model.HouseholdState(A).ShouldBe(true);
        model.HouseholdState(B).ShouldBe(false);

        model.SetParticipant(Alma.Id, false);
        model.ToggleHousehold(A);
        model.HouseholdState(A).ShouldBe(true);

        model.ToggleHousehold(A);
        model.HouseholdState(A).ShouldBe(false);
    }

    [Fact]
    public void Validation_lists_every_problem_in_danish()
    {
        var model = new ExpenseFormModel([A, B], Today, defaultPayerId: null) { Date = null, Amount = 10.005m };
        model.SelectAll(false);

        model.Validate().ShouldBe(
        [
            "Skriv hvad udgiften var til.",
            "Beløb må højst have to decimaler.",
            "Vælg en dato.",
            "Vælg hvem der betalte.",
            "Vælg mindst én at dele udgiften med.",
        ]);

        model.Amount = 0;
        model.Validate().ShouldContain("Beløbet skal være større end 0.");
    }

    [Fact]
    public void Builds_service_input_with_the_selected_people()
    {
        var model = NewModel();
        model.Description = "  Is  ";
        model.Amount = 85.50m;
        model.SetParticipant(Bo.Id, false);

        model.Validate().ShouldBeEmpty();
        model.ToInput().ShouldBe(new ExpenseInput("Is", 85.50m, Today, Anna.Id, [Anna.Id, Alma.Id]), new InputComparer());
    }

    [Fact]
    public void Editing_starts_from_the_existing_expense()
    {
        var expense = new ExpenseDto(
            Guid.NewGuid(), "Restaurant", 300m, Today.AddDays(-1), Bo.Id, "Bo", "Familie B", [Bo.Id], "Bo", "Bo", true);

        var model = ExpenseFormModel.ForEdit([A, B], expense);

        model.Description.ShouldBe("Restaurant");
        model.Amount.ShouldBe(300m);
        model.PaidByParticipantId.ShouldBe(Bo.Id);
        model.Date.ShouldBe(new DateTime(2026, 6, 30));
        model.IsSelected(Bo.Id).ShouldBeTrue();
        model.IsSelected(Anna.Id).ShouldBeFalse();
        model.HouseholdState(A).ShouldBe(false);
    }

    private sealed class InputComparer : IEqualityComparer<ExpenseInput>
    {
        public bool Equals(ExpenseInput? x, ExpenseInput? y) =>
            x is not null && y is not null
            && x with { SharedWithParticipantIds = null } == y with { SharedWithParticipantIds = null }
            && x.SharedWithParticipantIds!.SequenceEqual(y.SharedWithParticipantIds!);

        public int GetHashCode(ExpenseInput obj) => obj.Description.GetHashCode(StringComparison.Ordinal);
    }
}
