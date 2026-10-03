using Bunit;
using FamilyExpenses.Web.Components.UI;

namespace FamilyExpenses.Web.Tests;

public sealed class EventTabsTests : MudTestContext
{
    private static readonly Guid EventId = Guid.NewGuid();


    [Fact]
    public void Admin_sees_invitations_tab()
    {
        var cut = Render<EventTabs>(p => p.Add(t => t.EventId, EventId).Add(t => t.Active, "expenses").Add(t => t.IsAdmin, true));

        cut.FindAll("a").Select(a => a.GetAttribute("href")).ShouldBe(
        [
            $"events/{EventId}",
            $"events/{EventId}/expenses",
            $"events/{EventId}/households",
            $"events/{EventId}/settlement",
            $"events/{EventId}/invitations",
        ]);
        cut.Find($"a[href='events/{EventId}/expenses']").ClassList.ShouldContain("mud-button-filled");
    }

    [Fact]
    public void Member_does_not_see_invitations_tab()
    {
        var cut = Render<EventTabs>(p => p.Add(t => t.EventId, EventId).Add(t => t.Active, "overview"));

        cut.FindAll("a").Count.ShouldBe(4);
        cut.Markup.ShouldContain("Afregning");
        cut.Markup.ShouldNotContain("Invitationer");
    }
}
