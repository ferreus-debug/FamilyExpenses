using FamilyExpenses.Domain.Common;
using FamilyExpenses.Domain.Events;
using FamilyExpenses.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyExpenses.Infrastructure.Tests;

/// <summary>Phase 2 acceptance: data, users and login keys survive a restart.</summary>
public sealed class RestartTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async Task InitializeAsync() => _db = await TestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Data_users_and_data_protection_keys_survive_a_restart()
    {
        const string Email = "anna@example.com";
        const string Password = "hemmelig-kode";
        Guid eventId;
        string protectedPayload;

        await using (var scope = _db.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var result = await users.CreateAsync(new AppUser { UserName = Email, Email = Email, DisplayName = "Anna" }, Password);
            result.Succeeded.ShouldBeTrue(string.Join(", ", result.Errors.Select(e => e.Description)));

            var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("test");
            protectedPayload = protector.Protect("login-cookie");
        }

        var expenseEvent = new ExpenseEvent("Sommerhus");
        var family = expenseEvent.AddFamily("Familie A");
        var anna = expenseEvent.AddParticipant(family.Id, "Anna", ParticipantType.Adult);
        expenseEvent.AddExpense("Mad", new Money(99.95m), new DateOnly(2026, 7, 1), anna.Id, null, "u1");
        eventId = expenseEvent.Id;
        await using (var uow = _db.UnitOfWork.Create())
        {
            uow.Events.Add(expenseEvent);
            await uow.SaveChangesAsync();
        }

        await _db.RestartAsync();

        await using (var scope = _db.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByEmailAsync(Email)).ShouldNotBeNull();
            user.DisplayName.ShouldBe("Anna");
            (await users.CheckPasswordAsync(user, Password)).ShouldBeTrue();

            var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("test");
            protector.Unprotect(protectedPayload).ShouldBe("login-cookie");
        }

        await using (var uow = _db.UnitOfWork.Create())
        {
            var loaded = (await uow.Events.GetAsync(eventId)).ShouldNotBeNull();
            loaded.Expenses.ShouldHaveSingleItem().Amount.ShouldBe(new Money(99.95m));
        }
    }
}
