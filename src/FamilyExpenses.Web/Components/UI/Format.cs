using System.Globalization;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Web.Components.UI;

public static class Format
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    /// <summary>1234.5 → "1.234,50 kr."</summary>
    public static string Money(decimal amount) => amount.ToString("N2", Danish) + " kr.";

    /// <summary>2.5 → "2,5"</summary>
    public static string Weight(decimal weight) => weight.ToString("0.##", Danish);

    public static string Date(DateOnly date) => date.ToString("d. MMM yyyy", Danish);

    public static string Type(ParticipantType type) => type == ParticipantType.Adult ? "Voksen" : "Barn";
}
