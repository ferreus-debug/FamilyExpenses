using FamilyExpenses.Web.Components.Account;

namespace FamilyExpenses.Web.Tests;

public sealed class ReturnUrlsTests
{
    [Theory]
    [InlineData("/events/123", "/events/123")]
    [InlineData("invite/abc", "/invite/abc")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("https://evil.example.com", "/")]
    [InlineData("//evil.example.com", "/")]
    [InlineData("/\\evil.example.com", "/")]
    public void Only_local_return_urls_are_allowed(string? returnUrl, string expected) =>
        ReturnUrls.Safe(returnUrl).ShouldBe(expected);
}
