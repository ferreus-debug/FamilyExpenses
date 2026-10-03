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
