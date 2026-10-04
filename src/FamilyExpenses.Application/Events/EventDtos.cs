using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Application.Events;

public sealed record EventSummary(
    Guid Id,
    string Name,
    EventStatus Status,
    bool IsAdmin,
    string? MyHouseholdName,
    decimal Total);

/// <param name="Approvals">The households that must approve, and whether they have (empty while open).</param>
public sealed record EventDetails(
    Guid Id,
    string Name,
    string Currency,
    EventStatus Status,
    bool IsAdmin,
    Guid? MyHouseholdId,
    int MaxFamilies,
    IReadOnlyList<HouseholdDto> Households,
    IReadOnlyList<ApprovalDto> Approvals)
{
    public bool IsSettled => Status == EventStatus.Settled;

    /// <summary>Closed or settled: nothing can be changed until the admin reopens the event.</summary>
    public bool IsLocked => Status != EventStatus.Open;

    public IEnumerable<HouseholdDto> Families => Households.Where(h => h.Kind == HouseholdKind.Family);

    public HouseholdDto? ExtraPerson => Households.SingleOrDefault(h => h.Kind == HouseholdKind.ExtraPerson);

    public bool CanAddFamily => IsAdmin && !IsLocked && Families.Count() < MaxFamilies;

    public bool CanEditHousehold(Guid householdId) => !IsLocked && (IsAdmin || MyHouseholdId == householdId);
}

/// <param name="CanApprove">The current user may approve (or withdraw) for this household.</param>
public sealed record ApprovalDto(
    Guid HouseholdId,
    string Name,
    bool IsApproved,
    string? ApprovedByName,
    DateTimeOffset? ApprovedAt,
    bool CanApprove);

/// <param name="MemberNames">Display names of the users logged in as this household.</param>
public sealed record HouseholdDto(
    Guid Id,
    string Name,
    HouseholdKind Kind,
    decimal Weight,
    IReadOnlyList<ParticipantDto> Participants,
    IReadOnlyList<string> MemberNames);

public sealed record ParticipantDto(Guid Id, string Name, ParticipantType Type, decimal Weight);
