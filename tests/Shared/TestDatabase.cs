using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Infrastructure;
using FamilyExpenses.Infrastructure.Identity;
using FamilyExpenses.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FamilyExpenses.Tests.Shared;

/// <summary>
/// A real SQLite file wired up exactly like the app (AddInfrastructure + migrations).
/// <see cref="RestartAsync"/> builds a fresh service provider on the same file to simulate a restart.
/// Shared by the Infrastructure and Application test projects (linked source file).
/// </summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"familyexpenses-test-{Guid.NewGuid():N}.db");

    private readonly Action<IServiceCollection>? _configure;

    private TestDatabase(Action<IServiceCollection>? configure)
    {
        _configure = configure;
        Services = BuildServices();
    }

    public ServiceProvider Services { get; private set; }

    public IUnitOfWorkFactory UnitOfWork => Services.GetRequiredService<IUnitOfWorkFactory>();

    /// <param name="configure">Extra registrations, e.g. application services and a fake current user.</param>
    public static async Task<TestDatabase> CreateAsync(Action<IServiceCollection>? configure = null)
    {
        var database = new TestDatabase(configure);
        await database.Services.MigrateDatabaseAsync();
        return database;
    }

    public AppDbContext CreateContext() =>
        Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();

    /// <summary>Adds a login directly to the database and returns its id.</summary>
    public async Task<string> AddUserAsync(string displayName)
    {
        await using var context = CreateContext();
        var email = $"{displayName.ToLowerInvariant()}@example.com";
        var user = new AppUser { UserName = email, Email = email, DisplayName = displayName };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    public async Task RestartAsync()
    {
        await Services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Services = BuildServices();
        await Services.MigrateDatabaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    private ServiceProvider BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = $"Data Source={_path}",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        _configure?.Invoke(services);
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
