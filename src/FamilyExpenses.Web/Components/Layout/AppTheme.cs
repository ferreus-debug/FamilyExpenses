using MudBlazor;

namespace FamilyExpenses.Web.Components.Layout;

/// <summary>
/// Central MudBlazor theme so colors are defined in one place. Teal and coral on a warm off-white,
/// taken from the illustrations in wwwroot/img (generated with Higgsfield in the same palette).
/// </summary>
public static class AppTheme
{
    private const string Teal = "#0F766E";
    private const string Coral = "#F97316";

    public static MudTheme Default { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Teal,
            Secondary = Coral,
            Tertiary = "#F59E0B",
            AppbarBackground = Teal,
            Background = "#FBF7F0",
            Surface = "#FFFFFF",
            DrawerBackground = "#FFFDF9",
            TextPrimary = "#1F2933",
            TextSecondary = "#52606D",
            Success = "#15803D",
            Warning = "#C2410C",
            LinesDefault = "#E9E1D4",
            TableLines = "#EFE8DC",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#2DD4BF",
            Secondary = "#FB923C",
            Tertiary = "#FBBF24",
            AppbarBackground = "#0B3B37",
            Background = "#111A19",
            Surface = "#182422",
            DrawerBackground = "#14201E",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Nunito", "system-ui", "-apple-system", "Segoe UI", "sans-serif"],
            },
            H4 = new H4Typography { FontWeight = "800", LetterSpacing = "-.01em" },
            H5 = new H5Typography { FontWeight = "800" },
            H6 = new H6Typography { FontWeight = "700" },
            Button = new ButtonTypography { FontWeight = "700", TextTransform = "none" },
        },
    };
}
