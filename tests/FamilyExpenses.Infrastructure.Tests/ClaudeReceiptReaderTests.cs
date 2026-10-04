using FamilyExpenses.Infrastructure.Receipts;

namespace FamilyExpenses.Infrastructure.Tests;

public sealed class ClaudeReceiptReaderTests
{
    [Fact]
    public void Reads_the_fields_from_the_json_answer()
    {
        var reading = ClaudeReceiptReader.Parse(
            """{"isReceipt":true,"description":" Netto – dagligvarer ","total":312.75,"currency":"dkk","date":"2026-07-14"}""");

        reading.ShouldNotBeNull();
        reading.Description.ShouldBe("Netto – dagligvarer");
        reading.Total.ShouldBe(312.75m);
        reading.Currency.ShouldBe("DKK");
        reading.Date.ShouldBe(new DateOnly(2026, 7, 14));
    }

    [Fact]
    public void Unreadable_parts_become_null()
    {
        var reading = ClaudeReceiptReader.Parse(
            """{"isReceipt":true,"description":"","total":null,"currency":null,"date":"14-07-2026"}""");

        reading.ShouldNotBeNull();
        reading.Description.ShouldBeNull();
        reading.Total.ShouldBeNull();
        reading.Date.ShouldBeNull();
    }

    [Fact]
    public void A_photo_that_is_not_a_receipt_gives_nothing()
    {
        ClaudeReceiptReader.Parse("""{"isReceipt":false,"description":null,"total":null,"currency":null,"date":null}""")
            .ShouldBeNull();
    }
}
