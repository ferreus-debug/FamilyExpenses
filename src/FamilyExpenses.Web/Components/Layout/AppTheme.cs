using MudBlazor;

namespace FamilyExpenses.Web.Components.Layout;

/// <summary>Central MudBlazor theme so colors are defined in one place.</summary>
public static class AppTheme
{
    public static MudTheme Default { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#2E7D32",
            Secondary = "#00838F",
            AppbarBackground = "#2E7D32",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#66BB6A",
            Secondary = "#4DD0E1",
        },
    };
}
