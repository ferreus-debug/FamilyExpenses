namespace FamilyExpenses.Domain.Common;

internal static class Guard
{
    public const int MaxNameLength = 100;

    public static string Name(string? value, string what)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException($"{what} skal udfyldes.");
        }

        if (trimmed.Length > MaxNameLength)
        {
            throw new DomainException($"{what} må højst være {MaxNameLength} tegn.");
        }

        return trimmed;
    }
}
