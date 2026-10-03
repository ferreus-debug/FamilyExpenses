using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Common;

namespace FamilyExpenses.Application.Expenses;

/// <summary>
/// Photos on expenses (e.g. receipts). Every member can see them; adding and removing follows the
/// expense: its creator or the administrator, and not after the event is settled.
/// </summary>
public sealed class ExpensePictureService(IUnitOfWorkFactory uowFactory, ICurrentUser currentUser, TimeProvider time)
{
    /// <param name="image">JPEG, already shrunk by the browser.</param>
    /// <param name="thumbnail">Small JPEG for lists.</param>
    public async Task<Guid> AddAsync(
        Guid eventId,
        Guid expenseId,
        byte[] image,
        byte[] thumbnail,
        CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        EventAccess.RequireCanEdit(member, expenseEvent.GetExpense(expenseId));

        var existing = await uow.Pictures.CountForExpenseAsync(expenseId, cancellationToken);
        var picture = expenseEvent.CreatePicture(expenseId, existing, image, thumbnail, member.UserId, time.GetUtcNow());
        uow.Pictures.Add(picture);
        await uow.SaveChangesAsync(cancellationToken);
        return picture.Id;
    }

    public async Task RemoveAsync(Guid eventId, Guid pictureId, CancellationToken cancellationToken = default)
    {
        await using var uow = uowFactory.Create();
        var (expenseEvent, member) = await uow.LoadForMemberAsync(currentUser, eventId, cancellationToken);
        var owner = await uow.Pictures.GetOwnerAsync(pictureId, cancellationToken);
        if (owner is null || owner.EventId != eventId)
        {
            throw new NotFoundException("Billedet findes ikke.");
        }

        EventAccess.RequireCanEdit(member, expenseEvent.GetExpense(owner.ExpenseId));
        expenseEvent.EnsurePicturesCanChange();
        await uow.Pictures.DeleteAsync(pictureId, cancellationToken);
    }

    /// <summary>The JPEG bytes, or null if the picture doesn't exist or the user isn't a member of its event.</summary>
    public async Task<byte[]?> GetAsync(Guid pictureId, bool thumbnail, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        if (userId is null)
        {
            return null;
        }

        await using var uow = uowFactory.Create();
        var content = await uow.Pictures.GetContentAsync(pictureId, thumbnail, cancellationToken);
        if (content is null || await uow.Members.GetAsync(content.EventId, userId, cancellationToken) is null)
        {
            return null;
        }

        return content.Data;
    }
}
