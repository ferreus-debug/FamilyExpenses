using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Application.Events;
using FamilyExpenses.Application.Invitations;
using FamilyExpenses.Domain.Events;
using FamilyExpenses.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace FamilyExpenses.Application.Tests;

/// <summary>Application services on a real SQLite database, with a switchable logged-in user and a fake clock.</summary>
internal sealed class AppHarness : IAsyncDisposable
{
    public static readonly DateOnly Today = new(2026, 7, 1);

    private readonly AsyncServiceScope _scope;

    private AppHarness(TestDatabase db, FakeCurrentUser currentUser, FakeTimeProvider time)
    {
        Db = db;
        CurrentUser = currentUser;
        Time = time;
        _scope = db.Services.CreateAsyncScope();
    }

    public TestDatabase Db { get; }

    public FakeCurrentUser CurrentUser { get; }

    public FakeTimeProvider Time { get; }

    public EventService Events => Get<EventService>();

    public HouseholdService Households => Get<HouseholdService>();

    public Expenses.ExpenseService Expenses => Get<Expenses.ExpenseService>();

    public Settlements.SettlementService Settlements => Get<Settlements.SettlementService>();

    public InvitationService Invitations => Get<InvitationService>();

    public static async Task<AppHarness> CreateAsync()
    {
        var currentUser = new FakeCurrentUser();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));
        var db = await TestDatabase.CreateAsync(services =>
        {
            services.AddSingleton<ICurrentUser>(currentUser);
            services.AddSingleton<TimeProvider>(time);
            services.AddApplication();
        });
        return new AppHarness(db, currentUser, time);
    }

    public void LogInAs(string? userId) => CurrentUser.UserId = userId;

    /// <summary>
    /// The plan's worked example built through the services: Anna (admin, Familie A) creates the event,
    /// invites Familie B and C; Bo and Carla accept. Dorte is a stranger.
    /// </summary>
    public async Task<Scenario> CreateScenarioAsync()
    {
        var anna = await Db.AddUserAsync("Anna");
        var bo = await Db.AddUserAsync("Bo");
        var carla = await Db.AddUserAsync("Carla");
        var dorte = await Db.AddUserAsync("Dorte");

        LogInAs(anna);
        var eventId = await Events.CreateAsync("Sommerhus 2026");
        var a = await Households.AddFamilyAsync(eventId, "Familie A");
        var b = await Households.AddFamilyAsync(eventId, "Familie B");
        var c = await Households.AddFamilyAsync(eventId, "Familie C");
        await Events.JoinHouseholdAsync(eventId, a);

        foreach (var (household, name, type) in new[]
        {
            (a, "Anna", ParticipantType.Adult), (a, "Anders", ParticipantType.Adult),
            (a, "Alma", ParticipantType.Child), (a, "Arne", ParticipantType.Child),
            (b, "Bo", ParticipantType.Adult), (b, "Bente", ParticipantType.Adult), (b, "Bjørn", ParticipantType.Child),
            (c, "Carla", ParticipantType.Adult), (c, "Cilius", ParticipantType.Child), (c, "Cecilie", ParticipantType.Child),
        })
        {
            await Households.AddParticipantAsync(eventId, household, name, type);
        }

        var x = await Households.SetExtraPersonAsync(eventId, "Xenia", ParticipantType.Adult);

        var inviteB = await Invitations.CreateAsync(eventId, b);
        var inviteC = await Invitations.CreateAsync(eventId, c);
        LogInAs(bo);
        await Invitations.AcceptAsync(inviteB.Token);
        LogInAs(carla);
        await Invitations.AcceptAsync(inviteC.Token);

        LogInAs(anna);
        var details = await Events.GetAsync(eventId);
        var people = details.Households.SelectMany(h => h.Participants).ToDictionary(p => p.Name, p => p.Id);

        return new Scenario(eventId, a, b, c, x, people, anna, bo, carla, dorte);
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await Db.DisposeAsync();
    }

    public T Get<T>()
        where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();
}

internal sealed class FakeCurrentUser : ICurrentUser
{
    public string? UserId { get; set; }
}

internal sealed record Scenario(
    Guid EventId,
    Guid A,
    Guid B,
    Guid C,
    Guid X,
    IReadOnlyDictionary<string, Guid> People,
    string AnnaUser,
    string BoUser,
    string CarlaUser,
    string DorteUser)
{
    public Guid Person(string name) => People[name];
}
