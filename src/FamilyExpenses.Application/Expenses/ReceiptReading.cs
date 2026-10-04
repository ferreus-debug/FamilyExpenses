namespace FamilyExpenses.Application.Expenses;

/// <summary>What was read from a receipt. Each part is <c>null</c> when it couldn't be read.</summary>
/// <param name="Description">Short Danish description, e.g. "Netto – dagligvarer".</param>
/// <param name="Total">The total paid, in <paramref name="Currency"/>.</param>
/// <param name="Currency">ISO code, e.g. "DKK" or "EUR".</param>
public sealed record ReceiptReading(string? Description, decimal? Total, string? Currency, DateOnly? Date)
{
    public const string Kroner = "DKK";

    /// <summary>The total if it is in kroner; other currencies aren't converted.</summary>
    public decimal? TotalInKroner =>
        Total is > 0 && (Currency is null || string.Equals(Currency, Kroner, StringComparison.OrdinalIgnoreCase))
            ? decimal.Round(Total.Value, 2)
            : null;

    public bool IsForeignCurrency =>
        Total is > 0 && Currency is not null && !string.Equals(Currency, Kroner, StringComparison.OrdinalIgnoreCase);
}
