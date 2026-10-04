using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Common;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Application.Expenses;

/// <summary>
/// Fills in the expense form from a photo of the receipt. Only for logged-in users, since every
/// reading costs money; nothing is saved – the user checks the fields before saving the expense.
/// </summary>
public sealed class ReceiptService(IReceiptReader reader, ICurrentUser currentUser)
{
    public bool IsAvailable => reader.IsAvailable;

    /// <param name="jpeg">The photo, already shrunk by the browser.</param>
    /// <returns>What could be read, or <c>null</c> if nothing could.</returns>
    public async Task<ReceiptReading?> ReadAsync(byte[] jpeg, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jpeg);
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException();
        }

        if (!reader.IsAvailable || jpeg.Length > ExpensePicture.MaxImageBytes || !ExpensePicture.IsJpeg(jpeg))
        {
            return null;
        }

        var reading = await reader.ReadAsync(jpeg, cancellationToken);
        if (reading is null)
        {
            return null;
        }

        var description = reading.Description?.Trim();
        if (description?.Length > Expense.MaxDescriptionLength)
        {
            description = description[..Expense.MaxDescriptionLength];
        }

        return reading with { Description = string.IsNullOrEmpty(description) ? null : description };
    }
}
