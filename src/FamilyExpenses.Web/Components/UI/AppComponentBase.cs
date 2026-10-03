using FamilyExpenses.Application.Common;
using FamilyExpenses.Domain.Common;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FamilyExpenses.Web.Components.UI;

/// <summary>
/// Base for interactive pages: runs service calls and turns business errors into friendly snackbars,
/// so pages contain no try/catch boilerplate.
/// </summary>
public abstract class AppComponentBase : ComponentBase
{
    [Inject]
    protected ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    protected IDialogService Dialogs { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    protected bool Busy { get; private set; }

    /// <summary>Runs a change. Returns true on success; shows <paramref name="success"/> as a snackbar.</summary>
    protected async Task<bool> RunAsync(Func<Task> action, string? success = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        Busy = true;
        try
        {
            await action();
            if (success is not null)
            {
                Snackbar.Add(success, Severity.Success);
            }

            return true;
        }
        catch (DomainException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch (ForbiddenException ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        catch (NotFoundException ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
            Navigation.NavigateTo("/");
        }
        finally
        {
            Busy = false;
        }

        return false;
    }

    /// <summary>Loads data; returns default on error (after showing it).</summary>
    protected async Task<T?> LoadAsync<T>(Func<Task<T>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        T? result = default;
        await RunAsync(async () => result = await load());
        return result;
    }

    protected Task<bool?> ConfirmAsync(string title, string message, string yes = "Ja") =>
        Dialogs.ShowMessageBoxAsync(title, message, yesText: yes, cancelText: "Annullér");
}
