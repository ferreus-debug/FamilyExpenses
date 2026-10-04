namespace FamilyExpenses.Domain.Events;

public enum ParticipantType
{
    Adult,
    Child,

    /// <summary>Takes part but counts 0, so babies never get a share of the expenses.</summary>
    Baby,
}
