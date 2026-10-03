using FamilyExpenses.Domain.Access;
using FamilyExpenses.Domain.Events;
using FamilyExpenses.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FamilyExpenses.Infrastructure.Persistence;

/// <summary>
/// Single SQLite database for domain data, Identity and Data Protection keys,
/// so one file holds everything (simple backup on the Raspberry Pi).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options), IDataProtectionKeyContext
{
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
}
