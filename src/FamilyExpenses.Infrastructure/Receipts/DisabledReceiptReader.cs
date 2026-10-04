using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Expenses;

namespace FamilyExpenses.Infrastructure.Receipts;

/// <summary>Used when no API key is configured.</summary>
internal sealed class DisabledReceiptReader : IReceiptReader
{
    public bool IsAvailable => false;

    public Task<ReceiptReading?> ReadAsync(byte[] jpeg, CancellationToken cancellationToken = default) =>
        Task.FromResult<ReceiptReading?>(null);
}
