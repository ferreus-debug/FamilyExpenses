namespace FamilyExpenses.Domain.Access;

public enum MemberRole
{
    /// <summary>A family member: sees everything, edits own expenses.</summary>
    Member,

    /// <summary>Created the event: manages households, invitations and all expenses.</summary>
    Admin,
}
