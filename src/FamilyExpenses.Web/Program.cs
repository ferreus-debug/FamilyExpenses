using System.Globalization;
using FamilyExpenses.Application;
using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Infrastructure;
using FamilyExpenses.Web.Components;
using FamilyExpenses.Web.Components.Account;
using FamilyExpenses.Web.Hosting;
using FamilyExpenses.Web.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Type=notify under systemd on the Pi (readiness + clean shutdown); no-op elsewhere.
builder.Host.UseSystemd();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<RegistrationPolicy>();
builder.Services.AddScoped<FirebaseSignIn>();
builder.Services.Configure<FirebaseOptions>(builder.Configuration.GetSection(FirebaseOptions.SectionName));
builder.Services.AddSingleton(sp =>
    FirebaseTokenValidator.CreateConfigurationManager(sp.GetRequiredService<IOptions<FirebaseOptions>>().Value));
builder.Services.AddSingleton<IFirebaseTokenValidator, FirebaseTokenValidator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<CircuitHandler, UserCircuitHandler>());

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddReverseProxySupport(builder.Configuration);
builder.Services.AddHealthChecks().AddAppDatabaseCheck();

var app = builder.Build();

app.UseForwardedHeaders();

await app.Services.MigrateDatabaseAsync();

var danish = CultureInfo.GetCultureInfo("da-DK");
app.UseRequestLocalization(options =>
{
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(danish);
    options.SupportedCultures = [danish];
    options.SupportedUICultures = [danish];
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// No UseHttpsRedirection: Kestrel only listens on http://127.0.0.1 and Cloudflare enforces HTTPS
// ("Always Use HTTPS"). Forwarded headers above make requests through the tunnel count as https.

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapAccountEndpoints();
app.MapHealthChecks("/healthz");

await app.RunAsync();

/// <summary>Entry point; partial so integration tests can use WebApplicationFactory&lt;Program&gt;.</summary>
public partial class Program;
