using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Domain.Tests.Common;

public sealed class MoneyTests
{
    [Fact]
    public void Rejects_more_than_two_decimals() =>
        Should.Throw<DomainException>(() => new Money(1.005m));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(12.34, 1234)]
    [InlineData(-0.01, -1)]
    public void Converts_to_and_from_minor_units(decimal amount, long minorUnits)
    {
        new Money(amount).MinorUnits.ShouldBe(minorUnits);
        Money.FromMinorUnits(minorUnits).ShouldBe(new Money(amount));
    }

    [Fact]
    public void Supports_arithmetic_and_comparison()
    {
        var sum = new Money(10.50m) + new Money(0.25m);

        sum.ShouldBe(new Money(10.75m));
        (sum - new Money(0.75m)).ShouldBe(new Money(10m));
        (-sum).ShouldBe(new Money(-10.75m));
        (sum > Money.Zero).ShouldBeTrue();
    }
}
