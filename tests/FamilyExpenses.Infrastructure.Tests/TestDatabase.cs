using FamilyExpenses.Application.Abstractions;
using FamilyExpenses.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FamilyExpenses.Infrastructure.Tests;

/// <summary>
/// A real SQLite file wired up exactly like the app (AddInfrastructure + migrations).
/// <see cref="RestartAsync"/> builds a fresh service provider on the same file to simulate a restart.
/// </summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"familyexpenses-test-{Guid.NewGuid():N}.db");

    private TestDatabase()
    {
        Services = BuildServices();
    }

    public ServiceProvider Services { get; private set; }

    public IUnitOfWorkFactory UnitOfWork => Services.GetRequiredService<IUnitOfWorkFactory>();

    public static async Task<TestDatabase> CreateAsync()
    {
        var database = new TestDatabase();
        await database.Services.MigrateDatabaseAsync();
        return database;
    }

    public AppDbContext CreateContext() =>
        Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();

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
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
