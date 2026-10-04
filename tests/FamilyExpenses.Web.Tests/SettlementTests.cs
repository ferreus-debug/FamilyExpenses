using Bunit;
using FamilyExpenses.Application.Events;
using FamilyExpenses.Application.Settlements;
using FamilyExpenses.Domain.Events;
using FamilyExpenses.Web.Components.UI;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

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
        IsSettled: true,
        Balances:
        [
            new(A, "Familie A", 3.0m, 4250m, 3000m, 0m, 1250m, [new(Guid.NewGuid(), "Anna", 4250m)], []),
            new(B, "Familie B", 2.5m, 1700m, 2500m, 0m, -800m, [new(Guid.NewGuid(), "Bo", 1200m), new(Guid.NewGuid(), "Bente", 500m)], []),
            new(C, "Familie C", 2.0m, 2550m, 2000m, 0m, 550m, [new(Guid.NewGuid(), "Carla", 2550m)], []),
            new(X, "Xenia", 1.0m, 0m, 1000m, 0m, -1000m, [], []),
        ],
        Transfers:
        [
            new(X, "Xenia", A, "Familie A", 1000m, CanMarkPaid: false),
            new(B, "Familie B", C, "Familie C", 550m, CanMarkPaid: true),
            new(B, "Familie B", A, "Familie A", 250m, CanMarkPaid: true),
        ],
        Payments: []);

    // Familie B has paid Familie C; the rest is still open.
    private static readonly SettlementDto PartlyPaid = Example with
    {
        Balances =
        [
            Example.Balances[0],
            Example.Balances[1] with { Transferred = 550m, Balance = -250m },
            Example.Balances[2] with { Transferred = -550m, Balance = 0m },
            Example.Balances[3],
        ],
        Transfers = [Example.Transfers[0], Example.Transfers[2]],
        Payments = [new(Guid.NewGuid(), B, "Familie B", C, "Familie C", 550m, new DateOnly(2026, 7, 2), "Bo", CanRemove: true)],
    };


    [Fact]
    public void Shows_transfers_and_balances()
    {
        var cut = Render<SettlementSummary>(p => p.Add(s => s.Settlement, Example));

        cut.FindAll("[data-testid=transfer-amount]").Select(e => e.TextContent).ShouldBe(["1.000,00 kr.", "550,00 kr.", "250,00 kr."]);
        cut.FindAll("[data-testid=balance]").Select(e => e.TextContent.Trim())
            .ShouldBe(["+1.250,00 kr.", "-800,00 kr.", "+550,00 kr.", "-1.000,00 kr."]);
        cut.Markup.ShouldContain("Bo 1.200,00 kr., Bente 500,00 kr.");
        cut.Markup.ShouldNotContain("I skal betale");
        cut.FindAll("[data-testid=payment]").ShouldBeEmpty();
    }

    [Fact]
    public void Before_every_family_has_approved_only_the_preliminary_calculation_is_shown()
    {
        var cut = Render<SettlementSummary>(p => p.Add(s => s.Settlement, Example with { IsSettled = false }));

        cut.FindAll("[data-testid=transfer]").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("Hvem betaler hvem");
        cut.Markup.ShouldContain("Foreløbigt regnestykke");
        cut.FindAll("[data-testid=balance]").Count.ShouldBe(4);
    }

    [Fact]
    public void Open_event_explains_the_flow_and_lets_the_admin_close_it()
    {
        var closed = false;
        var cut = Render<ApprovalStatus>(p => p
            .Add(s => s.Status, EventStatus.Open)
            .Add(s => s.IsAdmin, true)
            .Add(s => s.OnClose, () => closed = true));

        cut.Find("[data-testid=status-open]").TextContent.ShouldContain("foreløbige");
        cut.Find("[data-testid=close-event]").Click();
        closed.ShouldBeTrue();

        Render<ApprovalStatus>(p => p.Add(s => s.Status, EventStatus.Open).Add(s => s.OnClose, () => { }))
            .FindAll("[data-testid=close-event]").ShouldBeEmpty();
    }

    [Fact]
    public void Closed_event_shows_who_has_approved_and_lets_my_family_approve()
    {
        ApprovalDto? approved = null;
        ApprovalDto? withdrawn = null;
        ApprovalDto[] approvals =
        [
            new(A, "Familie A", true, "Anna", DateTimeOffset.UnixEpoch, false),
            new(B, "Familie B", false, null, null, true),
            new(C, "Familie C", false, null, null, false),
        ];
        var cut = Render<ApprovalStatus>(p => p
            .Add(s => s.Status, EventStatus.Closed)
            .Add(s => s.Approvals, approvals)
            .Add(s => s.MyHouseholdId, B)
            .Add(s => s.OnApprove, (ApprovalDto a) => approved = a)
            .Add(s => s.OnWithdraw, (ApprovalDto a) => withdrawn = a));

        cut.Find("[data-testid=status-closed]").TextContent.ShouldContain("1 af 3 har godkendt");
        var rows = cut.FindAll("[data-testid=approval]");
        rows[0].TextContent.ShouldContain("Godkendt");
        rows[0].TextContent.ShouldContain("af Anna");
        rows[2].TextContent.ShouldContain("Mangler");
        cut.FindAll("[data-testid=reopen]").ShouldBeEmpty();

        var approve = cut.Find("[data-testid=approve]");
        approve.TextContent.ShouldContain("Godkend");
        approve.Click();
        approved.ShouldBe(approvals[1]);
        withdrawn.ShouldBeNull();
    }

    [Fact]
    public void Admin_approves_on_behalf_of_other_families_and_can_reopen()
    {
        var reopened = false;
        var cut = Render<ApprovalStatus>(p => p
            .Add(s => s.Status, EventStatus.Closed)
            .Add(s => s.IsAdmin, true)
            .Add(s => s.MyHouseholdId, A)
            .Add(s => s.Approvals, [new(A, "Familie A", true, "Anna", DateTimeOffset.UnixEpoch, true), new(B, "Familie B", false, null, null, true)])
            .Add(s => s.OnApprove, (ApprovalDto _) => { })
            .Add(s => s.OnWithdraw, (ApprovalDto _) => { })
            .Add(s => s.OnReopen, () => reopened = true));

        cut.Find("[data-testid=withdraw]").TextContent.ShouldContain("Træk tilbage");
        cut.Find("[data-testid=approve]").TextContent.ShouldContain("Godkend på deres vegne");
        cut.Find("[data-testid=reopen]").Click();
        reopened.ShouldBeTrue();
    }

    [Fact]
    public void Settled_event_shows_the_approvals_without_actions()
    {
        var cut = Render<ApprovalStatus>(p => p
            .Add(s => s.Status, EventStatus.Settled)
            .Add(s => s.Approvals, [new(A, "Familie A", true, "Anna", DateTimeOffset.UnixEpoch, true)])
            .Add(s => s.OnApprove, (ApprovalDto _) => { })
            .Add(s => s.OnWithdraw, (ApprovalDto _) => { }));

        cut.Find("[data-testid=status-settled]").TextContent.ShouldContain("Alle har godkendt – beløbene er endelige");
        cut.FindAll("[data-testid=approve], [data-testid=withdraw]").ShouldBeEmpty();
    }

    [Fact]
    public void Transfers_can_be_marked_as_paid_only_where_allowed()
    {
        TransferDto? marked = null;
        var cut = Render<SettlementSummary>(p => p
            .Add(s => s.Settlement, Example)
            .Add(s => s.OnMarkPaid, (TransferDto t) => marked = t));

        var buttons = cut.FindAll("[data-testid=mark-paid]");
        buttons.Count.ShouldBe(2);
        buttons[0].Click();

        marked.ShouldBe(Example.Transfers[1]);
    }

    [Fact]
    public void Shows_payments_made_and_what_is_left()
    {
        PaymentDto? removed = null;
        var cut = Render<SettlementSummary>(p => p
            .Add(s => s.Settlement, PartlyPaid)
            .Add(s => s.OnRemovePayment, (PaymentDto payment) => removed = payment));

        cut.FindAll("[data-testid=transfer-amount]").Select(e => e.TextContent).ShouldBe(["1.000,00 kr.", "250,00 kr."]);
        cut.Find("[data-testid=payment]").TextContent.ShouldContain("Familie B har betalt 550,00 kr. til Familie C");
        cut.FindAll("[data-testid=balance]").Select(e => e.TextContent.Trim())
            .ShouldBe(["+1.250,00 kr.", "-250,00 kr.", "0,00 kr.", "-1.000,00 kr."]);
        cut.Markup.ShouldContain("Overført");

        cut.Find("[data-testid=remove-payment]").Click();
        removed.ShouldBe(PartlyPaid.Payments[0]);
    }

    [Fact]
    public void Breakdown_link_raises_the_household()
    {
        HouseholdBalanceDto? shown = null;
        var cut = Render<SettlementSummary>(p => p
            .Add(s => s.Settlement, Example)
            .Add(s => s.OnShowBreakdown, (HouseholdBalanceDto b) => shown = b));

        cut.FindAll("[data-testid=show-breakdown]")[1].Click();

        shown.ShouldBe(Example.Balances[1]);
    }

    [Fact]
    public void Breakdown_dialog_explains_the_balance_expense_by_expense()
    {
        var balance = Example.Balances[1] with
        {
            Paid = 1200m,
            Share = 1550m,
            Transferred = 200m,
            Balance = -150m,
            Expenses =
            [
                new(Guid.NewGuid(), "Sommerhus", new DateOnly(2026, 7, 1), 0m, 1250m, 2.5m, 8.5m),
                new(Guid.NewGuid(), "Indkøb", new DateOnly(2026, 7, 2), 1200m, 300m, 0.5m, 2m),
                new(Guid.NewGuid(), "Gave", new DateOnly(2026, 7, 3), 0m, 0m, null, 1m),
            ],
        };
        var payment = new PaymentDto(Guid.NewGuid(), B, "Familie B", A, "Familie A", 200m, new DateOnly(2026, 7, 4), "Bo", true);

        var cut = Render<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        cut.InvokeAsync(() => dialogs.ShowAsync<BalanceBreakdownDialog>(
            "Udregning",
            new DialogParameters<BalanceBreakdownDialog> { { d => d.Balance, balance }, { d => d.Payments, [payment] } }));

        cut.FindAll("[data-testid=breakdown-weights]").Select(e => e.TextContent.Trim()).ShouldBe(
        [
            "1. jul. 2026 · vægt 2,5 af 8,5",
            "2. jul. 2026 · vægt 0,5 af 2",
            "3. jul. 2026 · lagt ud for andre",
        ]);
        var sum = cut.Find("[data-testid=breakdown-sum]").TextContent;
        sum.ShouldContain("Betalt til Familie A");
        sum.ShouldContain("-150,00 kr.");
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
    public void Text_for_messages_mentions_payments_already_made()
    {
        var text = SettlementText.Build("Sommerhus 2026", PartlyPaid);

        text.ShouldContain(
            """
            Familie B betaler 250,00 kr. til Familie A

            Allerede betalt:
            · Familie B har betalt 550,00 kr. til Familie C
            """.ReplaceLineEndings());
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
