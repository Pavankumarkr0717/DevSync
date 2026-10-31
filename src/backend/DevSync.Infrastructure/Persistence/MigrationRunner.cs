using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevSync.Infrastructure.Persistence;

/// <summary>Applies pending EF Core migrations at startup when enabled by configuration.</summary>
public static class MigrationRunner
{
    /// <summary>The 002 migration that deletes all 001 development data before reshaping users (spec 002 FR-030).</summary>
    public const string IdentityMigration = "IdentityAndPermissions";

    public static async Task ApplyAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevSyncDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MigrationRunner));

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database schema is up to date");
            return;
        }

        if (pending.Any(m => m.EndsWith("_" + IdentityMigration, StringComparison.Ordinal))
            && await TableExistsAsync(db, "users", cancellationToken))
        {
            var users = await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM users").SingleAsync(cancellationToken);
            var workspaces = await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM workspaces").SingleAsync(cancellationToken);
            logger.LogWarning(
                "Removing {Users} development users and {Workspaces} workspaces from 001 (spec 002 FR-030)",
                users, workspaces);
        }

        logger.LogInformation("Applying {MigrationCount} migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync(cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(DevSyncDbContext db, string table, CancellationToken cancellationToken) =>
        await db.Database
            .SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = {table}) AS \"Value\"")
            .SingleAsync(cancellationToken);
}
