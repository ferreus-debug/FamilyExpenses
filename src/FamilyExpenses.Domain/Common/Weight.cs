using System.Globalization;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Domain.Common;

/// <summary>How much a participant counts when expenses are split.</summary>
public readonly record struct Weight
{
    public Weight(decimal value)
    {
        if (value < 0m)
        {
            throw new DomainException("Vægt kan ikke være negativ.");
        }

        Value = value;
    }

    public static Weight Zero { get; } = new(0m);

    public static Weight Adult { get; } = new(1.0m);

    public static Weight Child { get; } = new(0.5m);

    public static Weight Baby { get; } = Zero;

    public decimal Value { get; }

    public static Weight For(ParticipantType type) => type switch
    {
        ParticipantType.Adult => Adult,
        ParticipantType.Child => Child,
        ParticipantType.Baby => Baby,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static Weight operator +(Weight left, Weight right) => new(left.Value + right.Value);

    public static Weight Add(Weight left, Weight right) => left + right;

    public override string ToString() => Value.ToString("0.0#", CultureInfo.InvariantCulture);
}

public static class WeightExtensions
{
    public static Weight Sum(this IEnumerable<Weight> weights) =>
        weights.Aggregate(Weight.Zero, (total, weight) => total + weight);
}
