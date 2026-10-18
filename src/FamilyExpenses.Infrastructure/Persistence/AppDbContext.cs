using FamilyExpenses.Domain.Access;
using FamilyExpenses.Domain.Events;
using FamilyExpenses.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FamilyExpenses.Infrastructure.Persistence;

/// <summary>
/// Single SQLite database for domain data, Identity and Data Protection keys,
/// so one file holds everything (simple backup on the Raspberry Pi).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options), IDataProtectionKeyContext
{
    /// <summary>Shadow concurrency token on Events, bumped on every save that changes the aggregate.</summary>
    internal const string EventVersion = "Version";

    public DbSet<ExpenseEvent> Events => Set<ExpenseEvent>();

    public DbSet<EventMember> EventMembers => Set<EventMember>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<ExpensePicture> ExpensePictures => Set<ExpensePicture>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BumpChangedEventVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        BumpChangedEventVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// A change anywhere in an event (an approval, an expense, a participant...) bumps the event's version, so
    /// the UPDATE of the Events row fails if another context saved the same event in the meantime. Without
    /// this, two contexts that each add a child row would never touch the event row and both would win.
    /// </summary>
    private void BumpChangedEventVersions()
    {
        ChangeTracker.DetectChanges();

        var changedEventIds = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(EventIdOf)
            .OfType<Guid>()
            .ToHashSet();

        foreach (var entry in ChangeTracker.Entries<ExpenseEvent>())
        {
            if ((entry.State is EntityState.Unchanged or EntityState.Modified) && changedEventIds.Contains(entry.Entity.Id))
            {
                var version = entry.Property<long>(EventVersion);
                version.CurrentValue = version.OriginalValue + 1;
                version.IsModified = true;
            }
        }
    }

    /// <summary>The event an entity of the ExpenseEvent aggregate belongs to; null for anything else.</summary>
    private Guid? EventIdOf(EntityEntry entry) => entry.Entity switch
    {
        ExpenseEvent expenseEvent => expenseEvent.Id,
        Household or Expense or Payment or HouseholdApproval => ShadowEventId(entry),
        Participant participant => ChangeTracker.Entries<Household>()
            .Where(h => h.Entity.Id == participant.HouseholdId)
            .Select(ShadowEventId)
            .FirstOrDefault(),
        _ => null,
    };

    private static Guid ShadowEventId(EntityEntry entry)
    {
        var eventId = entry.Property("EventId");
        return (Guid)(entry.State == EntityState.Added ? eventId.CurrentValue : eventId.OriginalValue)!;
    }
}
