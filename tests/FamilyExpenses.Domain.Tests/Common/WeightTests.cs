using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Domain.Tests.Common;

public sealed class WeightTests
{
    [Fact]
    public void Adult_counts_one_and_child_counts_half()
    {
        Weight.For(ParticipantType.Adult).Value.ShouldBe(1.0m);
        Weight.For(ParticipantType.Child).Value.ShouldBe(0.5m);
    }

    [Fact]
    public void Sums_weights() =>
        new[] { Weight.Adult, Weight.Adult, Weight.Child }.Sum().Value.ShouldBe(2.5m);
}
