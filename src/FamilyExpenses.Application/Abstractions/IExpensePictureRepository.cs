using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Application.Abstractions;

/// <summary>Picture bytes live outside the event aggregate, so they are only read when actually shown.</summary>
public interface IExpensePictureRepository
{
    /// <summary>Picture ids per expense in the event, oldest first. Expenses without pictures are left out.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> ListIdsByExpenseAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<int> CountForExpenseAsync(Guid expenseId, CancellationToken cancellationToken = default);

    /// <summary>Which event and expense a picture belongs to (no bytes), or null if it doesn't exist.</summary>
    Task<PictureOwner?> GetOwnerAsync(Guid pictureId, CancellationToken cancellationToken = default);

    /// <summary>The full image or the thumbnail, with the event it belongs to.</summary>
    Task<PictureContent?> GetContentAsync(Guid pictureId, bool thumbnail, CancellationToken cancellationToken = default);

    void Add(ExpensePicture picture);

    Task DeleteAsync(Guid pictureId, CancellationToken cancellationToken = default);
}

public sealed record PictureOwner(Guid EventId, Guid ExpenseId);

public sealed record PictureContent(Guid EventId, byte[] Data);
