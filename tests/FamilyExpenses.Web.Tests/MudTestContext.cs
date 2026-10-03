using Bunit;
using MudBlazor.Services;

namespace FamilyExpenses.Web.Tests;

/// <summary>
/// bUnit context with MudBlazor services. Disposes asynchronously: some MudBlazor services
/// (e.g. KeyInterceptorService used by MudChip) only implement IAsyncDisposable.
/// </summary>
public abstract class MudTestContext : BunitContext, IAsyncLifetime
{
    protected MudTestContext()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
}
