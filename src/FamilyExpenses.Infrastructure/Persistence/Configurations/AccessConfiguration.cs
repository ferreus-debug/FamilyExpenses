using FamilyExpenses.Domain.Access;
using FamilyExpenses.Domain.Events;
using FamilyExpenses.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyExpenses.Infrastructure.Persistence.Configurations;

internal sealed class EventMemberConfiguration : IEntityTypeConfiguration<EventMember>
{
    public void Configure(EntityTypeBuilder<EventMember> builder)
    {
        builder.ToTable("EventMembers");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(m => m.IsAdmin);
        builder.HasIndex(m => new { m.EventId, m.UserId }).IsUnique();

        builder.HasOne<ExpenseEvent>().WithMany().HasForeignKey(m => m.EventId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.Ignore(i => i.IsAccepted);

        builder.HasOne<ExpenseEvent>().WithMany().HasForeignKey(i => i.EventId).OnDelete(DeleteBehavior.Cascade);
    }
}
