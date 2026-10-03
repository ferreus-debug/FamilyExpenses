namespace FamilyExpenses.Application.Expenses;

/// <param name="SharedWithParticipantIds"><c>null</c> means everyone in the event right now.</param>
public sealed record ExpenseInput(
    string Description,
    decimal Amount,
    DateOnly Date,
    Guid PaidByParticipantId,
    IReadOnlyCollection<Guid>? SharedWithParticipantIds);

/// <param name="SharedWithSummary">E.g. "Alle" or "Familie A, Bo" (whole households shown by name).</param>
/// <param name="CanEdit">Whether the current user may edit or delete it (creator or administrator).</param>
/// <param name="PictureIds">Attached photos (e.g. receipts), oldest first; shown via /pictures/{id}.</param>
public sealed record ExpenseDto(
    Guid Id,
    string Description,
    decimal Amount,
    DateOnly Date,
    Guid PaidByParticipantId,
    string PaidByName,
    string PaidByHouseholdName,
    IReadOnlyList<Guid> SharedWithParticipantIds,
    string SharedWithSummary,
    string CreatedByName,
    bool CanEdit,
    IReadOnlyList<Guid> PictureIds);
