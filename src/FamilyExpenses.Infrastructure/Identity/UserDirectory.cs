using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyExpenses.Infrastructure.Identity;

internal sealed class UserDirectory(IDbContextFactory<AppDbContext> contextFactory) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var users = await db.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            u => u.Id,
            u => string.IsNullOrWhiteSpace(u.DisplayName) ? u.Email ?? "Ukendt" : u.DisplayName);
    }
}
