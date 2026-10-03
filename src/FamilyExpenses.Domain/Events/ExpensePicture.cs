using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Domain.Events;

/// <summary>
/// A photo attached to an expense, e.g. the receipt. Kept outside the <see cref="ExpenseEvent"/> aggregate so
/// loading an event never loads image bytes; created through <see cref="ExpenseEvent.CreatePicture"/> and
/// deleted together with its expense (database cascade).
/// </summary>
public sealed class ExpensePicture
{
    public const int MaxPerExpense = 5;

    /// <summary>The browser shrinks photos to at most 1600 px JPEG before upload, which is far below this.</summary>
    public const int MaxImageBytes = 3 * 1024 * 1024;

    public const int MaxThumbnailBytes = 300 * 1024;

    public const string ContentType = "image/jpeg";

    internal ExpensePicture(
        Guid eventId,
        Guid expenseId,
        byte[] image,
        byte[] thumbnail,
        string createdByUserId,
        DateTimeOffset createdAt)
    {
        Validate(image, MaxImageBytes);
        Validate(thumbnail, MaxThumbnailBytes);

        Id = Guid.NewGuid();
        EventId = eventId;
        ExpenseId = expenseId;
        Image = image;
        Thumbnail = thumbnail;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    // For EF Core.
    private ExpensePicture()
    {
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public Guid ExpenseId { get; private set; }

    public byte[] Image { get; private set; } = [];

    public byte[] Thumbnail { get; private set; } = [];

    public string CreatedByUserId { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>JPEG files start with FF D8 FF; anything else is refused, whatever the upload claims to be.</summary>
    public static bool IsJpeg(ReadOnlySpan<byte> data) =>
        data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;

    private static void Validate(byte[] data, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!IsJpeg(data))
        {
            throw new DomainException("Billedet kunne ikke læses. Prøv et andet billede.");
        }

        if (data.Length > maxBytes)
        {
            throw new DomainException("Billedet er for stort.");
        }
    }
}
