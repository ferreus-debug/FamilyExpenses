using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Web.Components.UI;

public sealed record ParticipantDialogResult(string Name, ParticipantType Type);
