using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyExpenses.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseEventConfiguration : IEntityTypeConfiguration<ExpenseEvent>
{
    public void Configure(EntityTypeBuilder<ExpenseEvent> builder)
    {
        builder.ToTable("Events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Name).HasMaxLength(Guard.MaxNameLength).IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(e => e.IsSettled);
        builder.Ignore(e => e.IsLocked);
        builder.Ignore(e => e.HouseholdsToApprove);

        builder.Ignore(e => e.Families);
        builder.Ignore(e => e.ExtraPerson);
        builder.Ignore(e => e.Participants);

        builder.HasMany(e => e.Households)
            .WithOne()
            .HasForeignKey("EventId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Households).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(e => e.Expenses)
            .WithOne()
            .HasForeignKey("EventId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Expenses).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(e => e.Payments)
            .WithOne()
            .HasForeignKey("EventId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(e => e.Approvals)
            .WithOne()
            .HasForeignKey("EventId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Approvals).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class HouseholdApprovalConfiguration : IEntityTypeConfiguration<HouseholdApproval>
{
    public void Configure(EntityTypeBuilder<HouseholdApproval> builder)
    {
        builder.ToTable("HouseholdApprovals");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.ApprovedByUserId).HasMaxLength(450).IsRequired();
        builder.HasOne<Household>().WithMany().HasForeignKey(a => a.HouseholdId).OnDelete(DeleteBehavior.ClientCascade);
    }
}

internal sealed class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.ToTable("Households");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.Name).HasMaxLength(Guard.MaxNameLength).IsRequired();
        builder.Property(h => h.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(h => h.TotalWeight);

        builder.HasMany(h => h.Participants)
            .WithOne()
            .HasForeignKey(p => p.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(h => h.Participants).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("Participants");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(Guard.MaxNameLength).IsRequired();
        builder.Property(p => p.Type).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(p => p.Weight);
    }
}

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Description).HasMaxLength(Expense.MaxDescriptionLength).IsRequired();

        // Stored as whole øre (INTEGER) so SQLite can sum and compare exactly.
        builder.Property(e => e.Amount)
            .HasConversion(money => money.MinorUnits, minorUnits => Money.FromMinorUnits(minorUnits))
            .HasColumnName("AmountMinorUnits");

        builder.Property(e => e.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(e => e.PaidByParticipantId);

        // Stored as a JSON array; the domain guarantees the ids belong to the event.
        builder.PrimitiveCollection(e => e.SharedWithParticipantIds)
            .HasField("_sharedWithParticipantIds")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        // Stored as whole øre (INTEGER) so SQLite can sum and compare exactly.
        builder.Property(p => p.Amount)
            .HasConversion(money => money.MinorUnits, minorUnits => Money.FromMinorUnits(minorUnits))
            .HasColumnName("AmountMinorUnits");

        builder.Property(p => p.CreatedByUserId).HasMaxLength(450).IsRequired();

        // The domain refuses to remove a household with payments, so the cascade only happens when the whole
        // event is deleted (EF then deletes the payments before the households).
        builder.HasOne<Household>().WithMany().HasForeignKey(p => p.FromHouseholdId).OnDelete(DeleteBehavior.ClientCascade);
        builder.HasOne<Household>().WithMany().HasForeignKey(p => p.ToHouseholdId).OnDelete(DeleteBehavior.ClientCascade);
    }
}

internal sealed class ExpensePictureConfiguration : IEntityTypeConfiguration<ExpensePicture>
{
    public void Configure(EntityTypeBuilder<ExpensePicture> builder)
    {
        builder.ToTable("ExpensePictures");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Image).IsRequired();
        builder.Property(p => p.Thumbnail).IsRequired();
        builder.Property(p => p.CreatedByUserId).HasMaxLength(450).IsRequired();

        // Deleting an expense (or the whole event) deletes its pictures in the database.
        builder.HasOne<Expense>()
            .WithMany()
            .HasForeignKey(p => p.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => p.EventId);
    }
}
