using FamilyExpenses.Domain.Access;
using FamilyExpenses.Domain.Common;

namespace FamilyExpenses.Domain.Tests.Access;

public sealed class InvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid HouseholdId = Guid.NewGuid();

    [Fact]
    public void Stores_only_a_hash_of_a_random_token()
    {
        var (invitation, token) = Invitation.Create(EventId, HouseholdId, Now);
        var (_, otherToken) = Invitation.Create(EventId, HouseholdId, Now);

        token.Length.ShouldBe(64);
        token.ShouldNotBe(otherToken);
        invitation.TokenHash.ShouldNotBe(token);
        invitation.TokenHash.ShouldBe(Invitation.HashToken(token));
        invitation.ExpiresAt.ShouldBe(Now + Invitation.DefaultLifetime);
    }

    [Fact]
    public void Accepting_grants_membership_of_the_household()
    {
        var (invitation, _) = Invitation.Create(EventId, HouseholdId, Now);

        var member = invitation.Accept("user-1", Now.AddDays(1));

        member.EventId.ShouldBe(EventId);
        member.HouseholdId.ShouldBe(HouseholdId);
        member.UserId.ShouldBe("user-1");
        member.Role.ShouldBe(MemberRole.Member);
        invitation.IsAccepted.ShouldBeTrue();
        invitation.AcceptedByUserId.ShouldBe("user-1");
    }

    [Fact]
    public void Can_only_be_used_once()
    {
        var (invitation, _) = Invitation.Create(EventId, HouseholdId, Now);
        invitation.Accept("user-1", Now);

        invitation.IsValid(Now).ShouldBeFalse();
        Should.Throw<DomainException>(() => invitation.Accept("user-2", Now));
    }

    [Fact]
    public void Expires()
    {
        var (invitation, _) = Invitation.Create(EventId, HouseholdId, Now, TimeSpan.FromHours(1));

        invitation.IsValid(Now.AddMinutes(59)).ShouldBeTrue();
        invitation.IsValid(Now.AddHours(1)).ShouldBeFalse();
        Should.Throw<DomainException>(() => invitation.Accept("user-1", Now.AddHours(1)));
    }

    [Fact]
    public void Family_member_must_have_a_household_but_admin_need_not()
    {
        Should.Throw<DomainException>(() => new EventMember(EventId, "user-1", null, MemberRole.Member));

        new EventMember(EventId, "user-1", null, MemberRole.Admin).IsAdmin.ShouldBeTrue();
    }
}
