using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace FamilyExpenses.Infrastructure.Persistence.Repositories;

internal sealed class ExpensePictureRepository(AppDbContext db) : IExpensePictureRepository
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> ListIdsByExpenseAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        // SQLite can't order by DateTimeOffset, so order the few rows here.
        var rows = await db.ExpensePictures
            .Where(p => p.EventId == eventId)
            .Select(p => new { p.Id, p.ExpenseId, p.CreatedAt })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ExpenseId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Guid>)[.. g.OrderBy(r => r.CreatedAt).Select(r => r.Id)]);
    }

    public Task<int> CountForExpenseAsync(Guid expenseId, CancellationToken cancellationToken = default) =>
        db.ExpensePictures.CountAsync(p => p.ExpenseId == expenseId, cancellationToken);

    public Task<PictureOwner?> GetOwnerAsync(Guid pictureId, CancellationToken cancellationToken = default) =>
        db.ExpensePictures
            .Where(p => p.Id == pictureId)
            .Select(p => new PictureOwner(p.EventId, p.ExpenseId))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PictureContent?> GetContentAsync(Guid pictureId, bool thumbnail, CancellationToken cancellationToken = default) =>
        thumbnail
            ? db.ExpensePictures.Where(p => p.Id == pictureId).Select(p => new PictureContent(p.EventId, p.Thumbnail)).SingleOrDefaultAsync(cancellationToken)
            : db.ExpensePictures.Where(p => p.Id == pictureId).Select(p => new PictureContent(p.EventId, p.Image)).SingleOrDefaultAsync(cancellationToken);

    public void Add(ExpensePicture picture) => db.ExpensePictures.Add(picture);

    public Task DeleteAsync(Guid pictureId, CancellationToken cancellationToken = default) =>
        db.ExpensePictures.Where(p => p.Id == pictureId).ExecuteDeleteAsync(cancellationToken);
}
