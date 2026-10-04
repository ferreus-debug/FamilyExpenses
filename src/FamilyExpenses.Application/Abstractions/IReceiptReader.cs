using FamilyExpenses.Application.Expenses;

namespace FamilyExpenses.Application.Abstractions;

/// <summary>Reads what a receipt photo says. Implemented by Infrastructure (Claude), or switched off.</summary>
public interface IReceiptReader
{
    /// <summary>False when no reader is configured; the form then hides the button.</summary>
    bool IsAvailable { get; }

    /// <param name="jpeg">The photo, already shrunk by the browser.</param>
    /// <returns>What could be read, or <c>null</c> if the photo isn't a readable receipt or the reader failed.</returns>
    Task<ReceiptReading?> ReadAsync(byte[] jpeg, CancellationToken cancellationToken = default);
}
