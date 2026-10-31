using DevSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace DevSync.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory against a real PostgreSQL database.
/// Defaults: Development environment, fixed test signing key, migrations applied on startup,
/// a generous per-IP auth rate limit (tests of the limit lower it explicitly).
/// </summary>
public sealed class DevSyncApiFactory(
    string connectionString,
    string environment = "Development",
    IReadOnlyDictionary<string, string?>? settings = null,
    Action<IServiceCollection>? configureServices = null,
    ILoggerProvider? loggerProvider = null,
    FakeTimeProvider? time = null)
    : WebApplicationFactory<Program>
{
    /// <summary>A fixed 32-byte key so tokens stay valid across factories on the same database.</summary>
    public static readonly string TestSigningKey = Convert.ToBase64String("devsync-integration-tests-key-32"u8.ToArray());

    public string ConnectionString { get; } = connectionString;

    public FakeTimeProvider? Time { get; } = time;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);

        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DevSync"] = ConnectionString,
            ["DevSync:Database:ApplyMigrationsOnStartup"] = "true",
            ["DevSync:Auth:SigningKey"] = TestSigningKey,
            ["DevSync:Auth:IpRateLimitPerMinute"] = "10000",
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(values));

        if (loggerProvider is not null)
        {
            builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider));
        }

        builder.ConfigureTestServices(services =>
        {
            if (Time is not null)
            {
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Time));
            }

            configureServices?.Invoke(services);
        });
    }

    /// <summary>A database context for arranging or inspecting data directly.</summary>
    public AsyncServiceScopeWithDb CreateDbScope()
    {
        var scope = Services.CreateAsyncScope();
        return new AsyncServiceScopeWithDb(scope, scope.ServiceProvider.GetRequiredService<DevSyncDbContext>());
    }

    public static async Task<DevSyncApiFactory> CreateAsync(
        PostgresFixture postgres,
        Action<IServiceCollection>? configureServices = null,
        ILoggerProvider? loggerProvider = null,
        FakeTimeProvider? time = null,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var factory = new DevSyncApiFactory(
            await postgres.CreateDatabaseAsync(),
            settings: settings,
            configureServices: configureServices,
            loggerProvider: loggerProvider,
            time: time);
        _ = factory.Server; // start the host (migrate) now
        return factory;
    }

    /// <summary>A fake clock starting at the real current time (access-token lifetimes are checked against it).</summary>
    public static FakeTimeProvider NewClock() => new(DateTimeOffset.UtcNow);

    /// <summary>A host whose database is unreachable. Migrations are off so it can start.</summary>
    public static DevSyncApiFactory CreateWithUnreachableDatabase() =>
        new(
            PostgresFixture.UnreachableConnectionString,
            settings: new Dictionary<string, string?> { ["DevSync:Database:ApplyMigrationsOnStartup"] = "false" });
}

public sealed class AsyncServiceScopeWithDb(AsyncServiceScope scope, DevSyncDbContext db) : IAsyncDisposable
{
    public DevSyncDbContext Db { get; } = db;

    public IServiceProvider Services => scope.ServiceProvider;

    public ValueTask DisposeAsync() => scope.DisposeAsync();
}
