using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Common;
using FamilyExpenses.Application.Expenses;

namespace FamilyExpenses.Application.Tests;

public sealed class ReceiptServiceTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    [Fact]
    public async Task Returns_what_the_reader_found_with_a_tidied_description()
    {
        var reader = new FakeReader(new ReceiptReading("  Netto  ", 99.5m, "DKK", new DateOnly(2026, 7, 1)));

        var reading = await new ReceiptService(reader, new User("u1")).ReadAsync(Jpeg);

        reading.ShouldBe(new ReceiptReading("Netto", 99.5m, "DKK", new DateOnly(2026, 7, 1)));
    }

    [Fact]
    public async Task Overlong_description_is_cut_to_fit_an_expense()
    {
        var reader = new FakeReader(new ReceiptReading(new string('x', 300), null, null, null));

        var reading = await new ReceiptService(reader, new User("u1")).ReadAsync(Jpeg);

        reading!.Description!.Length.ShouldBe(200);
    }

    [Fact]
    public async Task Anonymous_users_cannot_read_receipts()
    {
        var reader = new FakeReader(null);

        await Should.ThrowAsync<ForbiddenException>(() => new ReceiptService(reader, new User(null)).ReadAsync(Jpeg));
        reader.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Non_jpeg_bytes_are_not_sent_to_the_reader()
    {
        var reader = new FakeReader(new ReceiptReading("x", 1, null, null));

        (await new ReceiptService(reader, new User("u1")).ReadAsync([1, 2, 3])).ShouldBeNull();
        reader.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(null, 50, true, false)]
    [InlineData("DKK", 50, true, false)]
    [InlineData("EUR", 50, false, true)]
    [InlineData("EUR", null, false, false)]
    public void Only_kroner_totals_are_used(string? currency, int? total, bool hasKroner, bool foreign)
    {
        var reading = new ReceiptReading(null, total, currency, null);

        (reading.TotalInKroner is not null).ShouldBe(hasKroner);
        reading.IsForeignCurrency.ShouldBe(foreign);
    }

    private sealed class FakeReader(ReceiptReading? result) : IReceiptReader
    {
        public int Calls { get; private set; }

        public bool IsAvailable => true;

        public Task<ReceiptReading?> ReadAsync(byte[] jpeg, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class User(string? userId) : ICurrentUser
    {
        public string? UserId { get; } = userId;
    }
}
