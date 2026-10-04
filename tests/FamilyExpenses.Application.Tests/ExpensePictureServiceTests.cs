using FamilyExpenses.Application.Common;
using FamilyExpenses.Application.Expenses;
using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Application.Tests;

public sealed class ExpensePictureServiceTests : ServiceTestBase
{
    // Smallest bytes that pass the JPEG check (FF D8 FF …); the content itself isn't decoded.
    private static readonly byte[] Image = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
    private static readonly byte[] Thumbnail = [0xFF, 0xD8, 0xFF, 0xE0, 9];

    private ExpensePictureService Pictures => App.Get<ExpensePictureService>();

    [Fact]
    public async Task Picture_is_shown_on_the_expense_and_visible_to_every_member()
    {
        var expenseId = await AddExpenseAsBoAsync();

        var pictureId = await Pictures.AddAsync(S.EventId, expenseId, Image, Thumbnail);

        App.LogInAs(S.CarlaUser);
        (await App.Expenses.ListAsync(S.EventId)).Single(e => e.Id == expenseId).PictureIds.ShouldBe([pictureId]);
        (await Pictures.GetAsync(pictureId, thumbnail: false)).ShouldBe(Image);
        (await Pictures.GetAsync(pictureId, thumbnail: true)).ShouldBe(Thumbnail);
    }

    [Fact]
    public async Task Strangers_and_anonymous_users_cannot_see_a_picture()
    {
        var pictureId = await Pictures.AddAsync(S.EventId, await AddExpenseAsBoAsync(), Image, Thumbnail);

        App.LogInAs(S.DorteUser);
        (await Pictures.GetAsync(pictureId, thumbnail: false)).ShouldBeNull();
        App.LogInAs(null);
        (await Pictures.GetAsync(pictureId, thumbnail: true)).ShouldBeNull();
    }

    [Fact]
    public async Task Only_the_creator_or_the_admin_can_add_and_remove_pictures()
    {
        var expenseId = await AddExpenseAsBoAsync();
        var pictureId = await Pictures.AddAsync(S.EventId, expenseId, Image, Thumbnail);

        App.LogInAs(S.CarlaUser);
        await Should.ThrowAsync<ForbiddenException>(() => Pictures.AddAsync(S.EventId, expenseId, Image, Thumbnail));
        await Should.ThrowAsync<ForbiddenException>(() => Pictures.RemoveAsync(S.EventId, pictureId));

        App.LogInAs(S.AnnaUser);
        await Pictures.RemoveAsync(S.EventId, pictureId);
        (await Pictures.GetAsync(pictureId, thumbnail: false)).ShouldBeNull();
    }

    [Fact]
    public async Task Only_jpeg_is_accepted()
    {
        var expenseId = await AddExpenseAsBoAsync();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A];

        await Should.ThrowAsync<DomainException>(() => Pictures.AddAsync(S.EventId, expenseId, png, Thumbnail));
    }

    [Fact]
    public async Task An_expense_has_at_most_five_pictures()
    {
        var expenseId = await AddExpenseAsBoAsync();
        for (var i = 0; i < ExpensePicture.MaxPerExpense; i++)
        {
            await Pictures.AddAsync(S.EventId, expenseId, Image, Thumbnail);
        }

        await Should.ThrowAsync<DomainException>(() => Pictures.AddAsync(S.EventId, expenseId, Image, Thumbnail));
    }

    [Fact]
    public async Task Pictures_cannot_change_on_a_settled_event()
    {
        var expenseId = await AddExpenseAsBoAsync();
        var pictureId = await Pictures.AddAsync(S.EventId, expenseId, Image, Thumbnail);

        App.LogInAs(S.AnnaUser);
        await App.Events.CloseAsync(S.EventId);

        await Should.ThrowAsync<DomainException>(() => Pictures.AddAsync(S.EventId, expenseId, Image, Thumbnail));
        await Should.ThrowAsync<DomainException>(() => Pictures.RemoveAsync(S.EventId, pictureId));
    }

    [Fact]
    public async Task Deleting_the_expense_deletes_its_pictures()
    {
        var expenseId = await AddExpenseAsBoAsync();
        var pictureId = await Pictures.AddAsync(S.EventId, expenseId, Image, Thumbnail);

        await App.Expenses.RemoveAsync(S.EventId, expenseId);

        (await Pictures.GetAsync(pictureId, thumbnail: false)).ShouldBeNull();
    }

    [Fact]
    public async Task Picture_from_another_event_cannot_be_removed_through_this_one()
    {
        var pictureId = await Pictures.AddAsync(S.EventId, await AddExpenseAsBoAsync(), Image, Thumbnail);

        App.LogInAs(S.DorteUser);
        var otherEvent = await App.Events.CreateAsync("Dortes tur");

        await Should.ThrowAsync<NotFoundException>(() => Pictures.RemoveAsync(otherEvent, pictureId));
    }

    private async Task<Guid> AddExpenseAsBoAsync()
    {
        App.LogInAs(S.BoUser);
        return await App.Expenses.AddAsync(S.EventId, new("Indkøb", 300m, AppHarness.Today, S.Person("Bo"), null));
    }
}
