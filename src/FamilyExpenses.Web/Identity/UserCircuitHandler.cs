using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace FamilyExpenses.Web.Identity;

/// <summary>
/// Copies the authenticated user into <see cref="CurrentUser"/> for the lifetime of a Blazor circuit,
/// so application services can be used from interactive components.
/// See "Access server-side Blazor services from a different DI scope" in the ASP.NET Core docs.
/// </summary>
internal sealed class UserCircuitHandler(
    AuthenticationStateProvider authenticationStateProvider,
    CurrentUser currentUser) : CircuitHandler, IDisposable
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        authenticationStateProvider.AuthenticationStateChanged += OnAuthenticationChanged;
        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        currentUser.Principal = state.User;
    }

    public void Dispose() => authenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationChanged;

    private void OnAuthenticationChanged(Task<AuthenticationState> task)
    {
        _ = UpdateAsync();

        async Task UpdateAsync()
        {
            var state = await task;
            currentUser.Principal = state.User;
        }
    }
}
