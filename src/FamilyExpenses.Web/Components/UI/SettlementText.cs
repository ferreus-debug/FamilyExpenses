using System.Text;
using FamilyExpenses.Application.Settlements;

namespace FamilyExpenses.Web.Components.UI;

/// <summary>Plain-text settlement for pasting into SMS/Messenger.</summary>
public static class SettlementText
{
    public static string Build(string eventName, SettlementDto settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);

        var text = new StringBuilder();
        text.Append("Afregning: ").AppendLine(eventName);
        text.Append("Udgifter i alt: ").AppendLine(Format.Money(settlement.Total));
        text.AppendLine();

        if (settlement.Transfers.Count == 0)
        {
            text.AppendLine("Alle har betalt deres andel – ingen skal betale noget.");
        }
        else
        {
            foreach (var t in settlement.Transfers)
            {
                text.Append(t.FromName).Append(" betaler ").Append(Format.Money(t.Amount)).Append(" til ").AppendLine(t.ToName);
            }
        }

        text.AppendLine();
        text.AppendLine("Voksne tæller 1, børn 0,5.");
        foreach (var b in settlement.Balances)
        {
            text.Append("· ").Append(b.Name)
                .Append(": betalt ").Append(Format.Money(b.Paid))
                .Append(", andel ").Append(Format.Money(b.Share))
                .Append(" (vægt ").Append(Format.Weight(b.Weight)).AppendLine(")");
        }

        return text.ToString().TrimEnd();
    }
}
