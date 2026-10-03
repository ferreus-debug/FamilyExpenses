using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Domain.Events;

public sealed class Participant
{
    internal Participant(Guid householdId, string name, ParticipantType type)
    {
        Id = Guid.NewGuid();
        HouseholdId = householdId;
        Update(name, type);
    }

    public Guid Id { get; }

    public Guid HouseholdId { get; }

    public string Name { get; private set; } = string.Empty;

    public ParticipantType Type { get; private set; }

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
