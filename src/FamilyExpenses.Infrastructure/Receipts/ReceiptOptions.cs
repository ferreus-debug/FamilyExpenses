namespace FamilyExpenses.Infrastructure.Receipts;

/// <summary>Settings for reading receipts with Claude (section <c>Receipts</c>).</summary>
public sealed class ReceiptOptions
{
    public const string SectionName = "Receipts";

    /// <summary>Anthropic API key. Without it, receipt reading is switched off.</summary>
    public string? AnthropicApiKey { get; set; }

    public string Model { get; set; } = "claude-opus-5-5";
}
