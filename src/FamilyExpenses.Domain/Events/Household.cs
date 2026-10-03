using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Domain.Events;

/// <summary>A settlement unit: a family, or the single extra person.</summary>
public sealed class Household
{
    private readonly List<Participant> _participants = [];

    internal Household(string name, HouseholdKind kind, int sortOrder)
    {
        Id = Guid.NewGuid();
        Kind = kind;
        SortOrder = sortOrder;
        Rename(name);
    }

    // For EF Core.
    private Household()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public HouseholdKind Kind { get; private set; }

    /// <summary>Creation order. Keeps display and settlement rounding stable after loading from the database.</summary>
    public int SortOrder { get; private set; }

    public IReadOnlyList<Participant> Participants => _participants;

    public Weight TotalWeight => _participants.Select(p => p.Weight).Sum();

    internal void Rename(string name) => Name = Guard.Name(name, "Navn på husstand");

    internal Participant AddParticipant(string name, ParticipantType type)
    {
        var sortOrder = _participants.Count == 0 ? 0 : _participants.Max(p => p.SortOrder) + 1;
        var participant = new Participant(Id, name, type, sortOrder);
        _participants.Add(participant);
        return participant;
    }

    internal void RemoveParticipant(Participant participant) => _participants.Remove(participant);
}
