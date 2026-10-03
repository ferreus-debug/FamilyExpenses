using Bunit;
using Bunit.TestDoubles;
using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Events;
using FamilyExpenses.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyExpenses.Web.Tests;

public sealed class HomePageTests : MudTestContext
{
    private readonly BunitAuthorizationContext _auth;

    public HomePageTests()
    {
        _auth = AddAuthorization();
        Services.AddSingleton<IUnitOfWorkFactory>(new EmptyUnitOfWorkFactory());
        Services.AddSingleton<ICurrentUser>(new StaticCurrentUser("anna"));
        Services.AddSingleton<IUserDirectory>(new EmptyUserDirectory());
        Services.AddScoped<EventService>();
    }

    [Fact]
    public void Anonymous_visitor_sees_intro_and_login()
    {
        var cut = Render<Home>();

        cut.Find("h3").TextContent.ShouldBe("Del turen – ikke regnestykket");
        cut.Markup.ShouldContain("børn 0,5");
        cut.Find("img.fe-hero-image").GetAttribute("alt").ShouldNotBeNullOrWhiteSpace();
        cut.Find("a[href='Account/Login']").TextContent.ShouldContain("Log ind");
    }

    [Fact]
    public void Logged_in_user_without_events_is_told_how_to_start()
    {
        _auth.SetAuthorized("Anna");

        var cut = Render<Home>();

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Ingen ture endnu"));
        cut.Markup.ShouldContain("Ny begivenhed");
    }
}
