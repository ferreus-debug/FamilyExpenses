using System.Globalization;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Expenses;
using Microsoft.Extensions.Logging;

namespace FamilyExpenses.Infrastructure.Receipts;

/// <summary>Sends the receipt photo to Claude and gets the description, total and date back as JSON.</summary>
internal sealed partial class ClaudeReceiptReader(AnthropicClient client, ReceiptOptions options, ILogger<ClaudeReceiptReader> logger)
    : IReceiptReader
{
    private const string Prompt = """
        Billedet er (forhåbentlig) en kvittering fra et køb på en familietur. Udfyld felterne:
        - isReceipt: om billedet viser en kvittering eller regning.
        - description: kort dansk beskrivelse til en udgiftsliste, højst 60 tegn: butikkens navn og hvad der
          overvejende er købt, fx "Netto – dagligvarer" eller "Restaurant Havnen – aftensmad".
        - total: det samlede beløb der er betalt (efter rabat, inkl. moms), som tal med punktum som decimaltegn.
        - currency: valuta som ISO-kode, fx "DKK", "EUR" eller "SEK". "kr." på en dansk kvittering er DKK.
        - date: købsdatoen som ÅÅÅÅ-MM-DD. Danske kvitteringer skriver typisk DD-MM-ÅÅÅÅ eller DD.MM.ÅÅ.
        Brug null for det du ikke kan læse sikkert. Gæt ikke.
        """;

    private static readonly Dictionary<string, JsonElement> Schema = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            ["isReceipt"] = new { type = "boolean" },
            ["description"] = new { type = new[] { "string", "null" } },
            ["total"] = new { type = new[] { "number", "null" } },
            ["currency"] = new { type = new[] { "string", "null" } },
            ["date"] = new { type = new[] { "string", "null" } },
        }),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "isReceipt", "description", "total", "currency", "date" }),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };

    public bool IsAvailable => true;

    public async Task<ReceiptReading?> ReadAsync(byte[] jpeg, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.Beta.Messages.Create(
                new MessageCreateParams
                {
                    Model = options.Model,
                    MaxTokens = 4096,
                    // A receipt is easy; low effort keeps the wait short on a phone.
                    OutputConfig = new BetaOutputConfig
                    {
                        Effort = Effort.Low,
                        Format = new BetaJsonOutputFormat { Schema = Schema },
                    },
                    // If the model declines for policy reasons, the server retries on its default fallback model.
                    Betas = ["server-side-fallback-2026-07-01"],
                    Fallbacks = new Default(),
                    Messages =
                    [
                        new()
                        {
                            Role = Role.User,
                            Content = new List<BetaContentBlockParam>
                            {
                                new BetaImageBlockParam
                                {
                                    Source = new BetaBase64ImageSource
                                    {
                                        Data = Convert.ToBase64String(jpeg),
                                        MediaType = MediaType.ImageJpeg,
                                    },
                                },
                                new BetaTextBlockParam { Text = Prompt },
                            },
                        },
                    ],
                },
                cancellationToken);

            if (response.StopReason != "end_turn")
            {
                LogUnexpectedStop(response.StopReason?.ToString());
                return null;
            }

            var json = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
            return Parse(json);
        }
        catch (AnthropicApiException ex)
        {
            LogFailed(ex);
            return null;
        }
        catch (HttpRequestException ex)
        {
            LogFailed(ex);
            return null;
        }
        catch (JsonException ex)
        {
            LogFailed(ex);
            return null;
        }
    }

    internal static ReceiptReading? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("isReceipt", out var isReceipt) || !isReceipt.GetBoolean())
        {
            return null;
        }

        DateOnly? date = String(root, "date") is { } text
            && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
        decimal? total = root.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetDecimal() : null;

        return new ReceiptReading(String(root, "description"), total, String(root, "currency")?.ToUpperInvariant(), date);
    }

    private static string? String(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Receipt reading stopped early: {StopReason}")]
    private partial void LogUnexpectedStop(string? stopReason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Receipt reading failed")]
    private partial void LogFailed(Exception exception);
}
