using System.Globalization;
using FamilyExpenses.Application;
using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Infrastructure;
using FamilyExpenses.Web.Components;
using FamilyExpenses.Web.Identity;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<CircuitHandler, UserCircuitHandler>());

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

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
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
