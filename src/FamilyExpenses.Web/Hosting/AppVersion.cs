using System.Reflection;

namespace FamilyExpenses.Web.Hosting;

internal static class AppVersion
{
    /// <summary>The release name from the build (-p:InformationalVersion), e.g. "r12-b9886e9".</summary>
    public static string Current { get; } =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}
