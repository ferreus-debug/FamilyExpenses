using Bunit;
using FamilyExpenses.Web.Components.Pages;
using MudBlazor.Services;

namespace FamilyExpenses.Web.Tests;

public sealed class HomePageTests : BunitContext
{
    public HomePageTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_title_and_weighting_rule()
    {
        var cut = Render<Home>();

        cut.Find("h4").TextContent.ShouldBe("Fællesudgifter");
        cut.Markup.ShouldContain("børn tæller 0,5");
    }
}
