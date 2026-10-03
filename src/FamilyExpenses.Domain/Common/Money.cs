using System.Globalization;

namespace FamilyExpenses.Domain.Common;

/// <summary>An amount in the event's currency with at most two decimals (øre).</summary>
public readonly record struct Money : IComparable<Money>
{
    public Money(decimal amount)
    {
        if (decimal.Round(amount, 2) != amount)
        {
            throw new DomainException("Beløb må højst have to decimaler.");
        }

        Amount = amount;
    }

    public static Money Zero { get; } = new(0m);

    public decimal Amount { get; }

    /// <summary>The amount in minor units (øre). Used for exact integer arithmetic.</summary>
    public long MinorUnits => (long)(Amount * 100m);

    public static Money FromMinorUnits(long minorUnits) => new(minorUnits / 100m);

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    public static Money operator -(Money left, Money right) => new(left.Amount - right.Amount);

    public static Money operator -(Money value) => new(-value.Amount);

    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;

    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;

    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;

    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;

    public static Money Add(Money left, Money right) => left + right;

    public static Money Subtract(Money left, Money right) => left - right;

    public static Money Negate(Money value) => -value;

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public override string ToString() => Amount.ToString("N2", CultureInfo.InvariantCulture);
}
