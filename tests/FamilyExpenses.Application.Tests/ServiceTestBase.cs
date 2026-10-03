namespace FamilyExpenses.Application.Tests;

public abstract class ServiceTestBase : IAsyncLifetime
{
    internal AppHarness App { get; private set; } = null!;

    internal Scenario S { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        App = await AppHarness.CreateAsync();
        S = await App.CreateScenarioAsync();
    }

    public async Task DisposeAsync() => await App.DisposeAsync();
}
