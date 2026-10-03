using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Application.Events;

public sealed record EventSummary(
    Guid Id,
    string Name,
    bool IsSettled,
    bool IsAdmin,
    string? MyHouseholdName,
    decimal Total);

public sealed record EventDetails(
    Guid Id,
    string Name,
    string Currency,
    bool IsSettled,
    bool IsAdmin,
    Guid? MyHouseholdId,
    int MaxFamilies,
    IReadOnlyList<HouseholdDto> Households)
{
    public IEnumerable<HouseholdDto> Families => Households.Where(h => h.Kind == HouseholdKind.Family);

    public HouseholdDto? ExtraPerson => Households.SingleOrDefault(h => h.Kind == HouseholdKind.ExtraPerson);

    public bool CanAddFamily => IsAdmin && !IsSettled && Families.Count() < MaxFamilies;

    public bool CanEditHousehold(Guid householdId) => !IsSettled && (IsAdmin || MyHouseholdId == householdId);
}

/// <param name="MemberNames">Display names of the users logged in as this household.</param>
public sealed record HouseholdDto(
    Guid Id,
    string Name,
    HouseholdKind Kind,
    decimal Weight,
    IReadOnlyList<ParticipantDto> Participants,
    IReadOnlyList<string> MemberNames);

public sealed record ParticipantDto(Guid Id, string Name, ParticipantType Type, decimal Weight);
