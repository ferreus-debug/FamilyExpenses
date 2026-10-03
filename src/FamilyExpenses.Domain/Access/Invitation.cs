using System.Security.Cryptography;
using System.Text;
using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Domain.Access;

/// <summary>
/// A one-time invitation that lets a family create a login tied to their household.
/// Only a hash of the token is stored, so a database leak does not leak usable links.
/// </summary>
public sealed class Invitation
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(14);

    private Invitation(Guid eventId, Guid householdId, string tokenHash, DateTimeOffset expiresAt)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        HouseholdId = householdId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    // For EF Core.
    private Invitation()
    {
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public Guid HouseholdId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public string? AcceptedByUserId { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public bool IsAccepted => AcceptedAt is not null;

    /// <summary>Creates an invitation and returns the raw token to put in the link (shown once).</summary>
    public static (Invitation Invitation, string Token) Create(
        Guid eventId,
        Guid householdId,
        DateTimeOffset now,
        TimeSpan? lifetime = null)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var invitation = new Invitation(eventId, householdId, HashToken(token), now + (lifetime ?? DefaultLifetime));
        return (invitation, token);
    }

    public static string HashToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public bool IsValid(DateTimeOffset now) => !IsAccepted && now < ExpiresAt;

    /// <summary>Marks the invitation as used and returns the membership it grants.</summary>
    public EventMember Accept(string userId, DateTimeOffset now)
    {
        if (IsAccepted)
        {
            throw new DomainException("Invitationen er allerede brugt.");
        }

        if (now >= ExpiresAt)
        {
            throw new DomainException("Invitationen er udløbet.");
        }

        AcceptedByUserId = userId;
        AcceptedAt = now;
        return new EventMember(EventId, userId, HouseholdId, MemberRole.Member);
    }
}
