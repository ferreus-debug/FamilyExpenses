using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Domain.Events;

/// <summary>A settlement unit: a family, or the single extra person.</summary>
public sealed class Household
{
    private readonly List<Participant> _participants = [];

    internal Household(string name, HouseholdKind kind)
    {
        Id = Guid.NewGuid();
        Kind = kind;
        Rename(name);
    }

    public Guid Id { get; }

    public string Name { get; private set; } = string.Empty;

    public HouseholdKind Kind { get; }

    public IReadOnlyList<Participant> Participants => _participants;

    public Weight TotalWeight => _participants.Select(p => p.Weight).Sum();

    internal void Rename(string name) => Name = Guard.Name(name, "Navn på husstand");

    internal Participant AddParticipant(string name, ParticipantType type)
    {
        var participant = new Participant(Id, name, type);
        _participants.Add(participant);
        return participant;
    }

    internal void RemoveParticipant(Participant participant) => _participants.Remove(participant);
}
