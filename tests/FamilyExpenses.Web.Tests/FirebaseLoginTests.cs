using System.Net;
using System.Text.RegularExpressions;
using FamilyExpenses.Web.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyExpenses.Web.Tests;

/// <summary>
/// POST /Account/FirebaseLogin through the real app, with Google's token check replaced by a fake:
/// who gets an account, who gets signed in, and who is turned away.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Disposed in IAsyncLifetime.DisposeAsync.")]
public sealed partial class FirebaseLoginTests : IAsyncLifetime
{
    private const string LoginCookie = ".AspNetCore.Identity.Application";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"familyexpenses-login-{Guid.NewGuid():N}.db");
    private WebApplicationFactory<Program> _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
            builder.ConfigureTestServices(services => services.AddSingleton<IFirebaseTokenValidator, FakeGoogle>());
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    [Fact]
    public async Task First_person_gets_an_account_and_is_signed_in()
    {
        var response = await SignInAsync("anna");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/");
        SetsLoginCookie(response).ShouldBeTrue();
    }

    [Fact]
    public async Task Stranger_without_an_invitation_is_turned_away()
    {
        await SignInAsync("anna");

        var response = await SignInAsync("bo");

        response.Headers.Location!.OriginalString.ShouldStartWith("/Account/Login?error=not-invited");
        SetsLoginCookie(response).ShouldBeFalse();
    }

    [Fact]
    public async Task Returning_person_is_signed_in_and_sent_back()
    {
        await SignInAsync("anna");

        var response = await SignInAsync("anna", returnUrl: "/events");

        response.Headers.Location!.OriginalString.ShouldBe("/events");
        SetsLoginCookie(response).ShouldBeTrue();
    }

    [Fact]
    public async Task Untrusted_token_is_refused()
    {
        var response = await SignInAsync("forged");

        response.Headers.Location!.OriginalString.ShouldStartWith("/Account/Login?error=failed");
        SetsLoginCookie(response).ShouldBeFalse();
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        using var client = Client();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["idToken"] = "anna" });

        var response = await client.PostAsync(new Uri("/Account/FirebaseLogin", UriKind.Relative), form);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<HttpResponseMessage> SignInAsync(string idToken, string returnUrl = "/")
    {
        using var client = Client();
        var loginPage = await client.GetStringAsync(new Uri("/Account/Login", UriKind.Relative));
        var antiforgery = AntiforgeryField().Match(loginPage).Groups[1].Value;

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery,
            ["idToken"] = idToken,
            ["returnUrl"] = returnUrl,
            ["invite"] = "",
        });
        return await client.PostAsync(new Uri("/Account/FirebaseLogin", UriKind.Relative), form);
    }

    private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static bool SetsLoginCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(c => c.StartsWith(LoginCookie + "=", StringComparison.Ordinal));

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryField();

    private sealed class FakeGoogle : IFirebaseTokenValidator
    {
        public Task<FirebaseIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(idToken switch
            {
                "anna" => new FirebaseIdentity("uid-anna", "anna@example.com", true, "Anna"),
                "bo" => new FirebaseIdentity("uid-bo", "bo@example.com", true, "Bo"),
                _ => null,
            });
    }
}
