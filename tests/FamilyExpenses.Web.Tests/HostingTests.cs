using System.Net;
using FamilyExpenses.Web.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace FamilyExpenses.Web.Tests;

/// <summary>
/// Runs the real app on Kestrel (loopback, like cloudflared on the Pi) in Production mode and checks
/// the behaviour that matters behind Cloudflare Tunnel.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Disposed in IAsyncLifetime.DisposeAsync.")]
public sealed class HostingTests : IAsyncLifetime
{
    // cloudflared passes the public host name through; HSTS is never sent for localhost/127.0.0.1.
    private const string PublicHost = "udgifter.example.com";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"familyexpenses-hosting-{Guid.NewGuid():N}.db");
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        var port = FreePort();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
            builder.UseSetting(WebHostDefaults.ServerUrlsKey, $"http://127.0.0.1:{port}");
        });
        _factory.UseKestrel();
        _factory.StartServer();

        // Own client: real loopback TCP connection (like cloudflared), no system proxy, no redirects.
        _client = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false, UseCookies = false })
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}"),
        };
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy_database()
    {
        var response = await _client.GetAsync(new Uri("/healthz", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
        File.Exists(_dbPath).ShouldBeTrue();
    }

    [Fact]
    public async Task Version_endpoint_reports_the_build_version()
    {
        // The deploy compares this with the release name CI stamped in (r<run>-<sha>).
        var response = await _client.GetAsync(new Uri("/version", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe(AppVersion.Current);
        AppVersion.Current.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Pictures_require_login()
    {
        var response = await _client.GetAsync(new Uri($"/pictures/{Guid.NewGuid()}", UriKind.Relative));

        // Sent to the login page, never the picture.
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldContain("/Account/Login");
    }

    [Fact]
    public async Task Requests_through_the_tunnel_are_treated_as_https()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        request.Headers.Host = PublicHost;
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AntiforgeryCookie(response).ShouldContain("secure", Case.Insensitive);
        response.Headers.Contains("Strict-Transport-Security").ShouldBeTrue();
    }

    [Fact]
    public async Task Plain_local_http_is_not_redirected_and_cookies_are_not_marked_secure()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        request.Headers.Host = PublicHost;

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AntiforgeryCookie(response).ShouldNotContain("secure", Case.Insensitive);
        response.Headers.Contains("Strict-Transport-Security").ShouldBeFalse();
    }

    private static int FreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string AntiforgeryCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal));
}
