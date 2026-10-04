using Bunit;
using FamilyExpenses.Application.Settlements;
using FamilyExpenses.Web.Components.UI;

namespace FamilyExpenses.Web.Tests;

public sealed class SettlementTests : MudTestContext
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();
    private static readonly Guid X = Guid.NewGuid();

    // The worked example from docs/PLAN.md.
    private static readonly SettlementDto Example = new(
        Total: 8500m,
        TotalWeight: 8.5m,
        IsSettled: false,
        Balances:
        [
            new(A, "Familie A", 3.0m, 4250m, 3000m, 1250m, [new(Guid.NewGuid(), "Anna", 4250m)]),
            new(B, "Familie B", 2.5m, 1700m, 2500m, -800m, [new(Guid.NewGuid(), "Bo", 1200m), new(Guid.NewGuid(), "Bente", 500m)]),
            new(C, "Familie C", 2.0m, 2550m, 2000m, 550m, [new(Guid.NewGuid(), "Carla", 2550m)]),
            new(X, "Xenia", 1.0m, 0m, 1000m, -1000m, []),
        ],
        Transfers:
        [
            new(X, "Xenia", A, "Familie A", 1000m),
            new(B, "Familie B", C, "Familie C", 550m),
            new(B, "Familie B", A, "Familie A", 250m),
        ]);


    [Fact]
    public void Shows_transfers_and_balances()
    {
        var cut = Render<SettlementSummary>(p => p.Add(s => s.Settlement, Example));

        cut.FindAll("[data-testid=transfer-amount]").Select(e => e.TextContent).ShouldBe(["1.000,00 kr.", "550,00 kr.", "250,00 kr."]);
        cut.FindAll("[data-testid=balance]").Select(e => e.TextContent.Trim())
            .ShouldBe(["+1.250,00 kr.", "-800,00 kr.", "+550,00 kr.", "-1.000,00 kr."]);
        cut.Markup.ShouldContain("Bo 1.200,00 kr., Bente 500,00 kr.");
        cut.Markup.ShouldNotContain("I skal betale");
    }

    [Fact]
    public void Highlights_what_my_household_pays_and_receives()
    {
        var cut = Render<SettlementSummary>(p => p.Add(s => s.Settlement, Example).Add(s => s.MyHouseholdId, B));

        var transfers = cut.FindAll("[data-testid=transfer]");
        transfers[0].TextContent.ShouldNotContain("I skal");
        transfers[1].TextContent.ShouldContain("I skal betale");
        transfers[2].TextContent.ShouldContain("I skal betale");
        cut.Markup.ShouldContain("Familie B (jer)");
    }

    [Fact]
    public void Explains_when_nothing_needs_to_be_paid()
    {
        var even = Example with { Transfers = [] };
        var empty = Example with { Total = 0, Transfers = [] };

        Render<SettlementSummary>(p => p.Add(s => s.Settlement, even))
            .Find("[data-testid=no-transfers]").TextContent.ShouldContain("ingen skal betale noget");
        Render<SettlementSummary>(p => p.Add(s => s.Settlement, empty))
            .Find("[data-testid=no-transfers]").TextContent.ShouldContain("ingen udgifter endnu");
    }

    [Fact]
    public void Text_for_messages_lists_transfers_and_the_calculation()
    {
        var text = SettlementText.Build("Sommerhus 2026", Example);

        text.ShouldBe(
            """
            Afregning: Sommerhus 2026
            Udgifter i alt: 8.500,00 kr.

            Xenia betaler 1.000,00 kr. til Familie A
            Familie B betaler 550,00 kr. til Familie C
            Familie B betaler 250,00 kr. til Familie A

            Voksne tæller 1, børn 0,5, babyer 0.
            · Familie A: betalt 4.250,00 kr., andel 3.000,00 kr. (vægt 3)
            · Familie B: betalt 1.700,00 kr., andel 2.500,00 kr. (vægt 2,5)
            · Familie C: betalt 2.550,00 kr., andel 2.000,00 kr. (vægt 2)
            · Xenia: betalt 0,00 kr., andel 1.000,00 kr. (vægt 1)
            """.ReplaceLineEndings());
    }
}
