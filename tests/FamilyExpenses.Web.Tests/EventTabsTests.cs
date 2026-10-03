using Bunit;
using FamilyExpenses.Web.Components.UI;
using MudBlazor.Services;

namespace FamilyExpenses.Web.Tests;

public sealed class EventTabsTests : BunitContext
{
    private static readonly Guid EventId = Guid.NewGuid();

    public EventTabsTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Admin_sees_invitations_tab()
    {
        var cut = Render<EventTabs>(p => p.Add(t => t.EventId, EventId).Add(t => t.Active, "expenses").Add(t => t.IsAdmin, true));

        cut.FindAll("a").Select(a => a.GetAttribute("href")).ShouldBe(
        [
            $"events/{EventId}",
            $"events/{EventId}/expenses",
            $"events/{EventId}/households",
            $"events/{EventId}/invitations",
        ]);
        cut.Find($"a[href='events/{EventId}/expenses']").ClassList.ShouldContain("mud-button-filled");
    }

    [Fact]
    public void Member_does_not_see_invitations_tab()
    {
        var cut = Render<EventTabs>(p => p.Add(t => t.EventId, EventId).Add(t => t.Active, "overview"));

        cut.FindAll("a").Count.ShouldBe(3);
        cut.Markup.ShouldNotContain("Invitationer");
    }
}
