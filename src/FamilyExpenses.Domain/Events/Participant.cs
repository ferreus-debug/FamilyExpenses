using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Domain.Events;

public sealed class Participant
{
    internal Participant(Guid householdId, string name, ParticipantType type, int sortOrder)
    {
        Id = Guid.NewGuid();
        HouseholdId = householdId;
        SortOrder = sortOrder;
        Update(name, type);
    }

    // For EF Core.
    private Participant()
    {
    }

    public Guid Id { get; private set; }

    public Guid HouseholdId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public ParticipantType Type { get; private set; }

    /// <summary>Creation order within the household.</summary>
    public int SortOrder { get; private set; }

    public Weight Weight => Weight.For(Type);

    internal void Update(string name, ParticipantType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Ukendt deltagertype.");
        }

        Name = Guard.Name(name, "Navn");
        Type = type;
    }
}
