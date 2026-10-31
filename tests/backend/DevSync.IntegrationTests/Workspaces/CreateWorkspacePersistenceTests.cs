using System.Data.Common;
using System.Net.Http.Json;
using DevSync.Infrastructure.Persistence;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DevSync.IntegrationTests.Workspaces;

/// <summary>US1 persistence guarantees: atomicity, identifier uniqueness, DB backstops, restarts, outages.</summary>
public sealed class CreateWorkspacePersistenceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Failure_after_the_workspace_insert_rolls_back_the_whole_create()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(
            postgres,
            services => services.ConfigureDbContext<DevSyncDbContext>(o => o.AddInterceptors(new FailAfterMembershipInsert())));
        var user = await factory.CreateAccountAsync();

        var response = await (await factory.ClientForAsync(user)).PostAsJsonAsync("/api/workspaces", new { name = "Doomed" });

        var problem = await response.AssertProblemAsync(500, "internal_error");
        Assert.DoesNotContain("Simulated", problem.ToString());
        await using var scope = factory.CreateDbScope();
        Assert.Equal(0, await scope.Db.Workspaces.CountAsync());
        Assert.Equal(0, await scope.Db.WorkspaceMemberships.CountAsync());
    }

    [Fact]
    public async Task A_duplicate_workspace_id_is_rejected_by_the_database_and_the_original_is_unchanged()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var user = await factory.CreateAccountAsync();
        var created = await (await factory.ClientForAsync(user)).CreateWorkspaceAsync("Original");
        var id = created.GetProperty("id").GetGuid();

        await using var scope = factory.CreateDbScope();
        var error = await Assert.ThrowsAsync<PostgresException>(() => scope.Db.Database.ExecuteSqlAsync($"""
            INSERT INTO workspaces (id, name, owner_id, created_at, updated_at)
            VALUES ({id}, 'Impostor', {user}, now(), now())
            """));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        var stored = await scope.Db.Workspaces.AsNoTracking().SingleAsync(w => w.Id == id);
        Assert.Equal("Original", stored.Name);
    }

    [Fact]
    public async Task The_database_rejects_a_blank_name_even_if_validation_is_bypassed()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var user = await factory.CreateAccountAsync();

        await using var scope = factory.CreateDbScope();
        var error = await Assert.ThrowsAsync<PostgresException>(() => scope.Db.Database.ExecuteSqlAsync($"""
            INSERT INTO workspaces (id, name, owner_id, created_at, updated_at)
            VALUES ({Guid.CreateVersion7()}, '   ', {user}, now(), now())
            """));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task Created_workspace_survives_an_application_restart()
    {
        string connectionString;
        Guid id;
        Guid user;
        await using (var first = await DevSyncApiFactory.CreateAsync(postgres))
        {
            connectionString = first.ConnectionString;
            user = await first.CreateAccountAsync();
            id = (await (await first.ClientForAsync(user)).CreateWorkspaceAsync("Durable", "Still here")).GetProperty("id").GetGuid();
        }

        await using var second = new DevSyncApiFactory(connectionString);
        await using var scope = second.CreateDbScope();
        var workspace = await scope.Db.Workspaces.AsNoTracking().Include(w => w.Members).SingleAsync(w => w.Id == id);

        Assert.Equal("Durable", workspace.Name);
        Assert.Equal("Still here", workspace.Description);
        Assert.Equal(user, workspace.OwnerId);
        Assert.Equal(user, Assert.Single(workspace.Members).UserId);
    }

    [Fact]
    public async Task Create_returns_503_when_the_database_is_unreachable()
    {
        await using var factory = DevSyncApiFactory.CreateWithUnreachableDatabase();

        var response = await factory.ClientWithUnverifiableSession().PostAsJsonAsync("/api/workspaces", new { name = "Offline" });

        var problem = await response.AssertProblemAsync(503, "service_unavailable");
        Assert.DoesNotContain("Npgsql", problem.ToString());
        Assert.DoesNotContain("127.0.0.1", problem.ToString());
    }

    /// <summary>Throws after the membership INSERT has run inside the SaveChanges transaction.</summary>
    private sealed class FailAfterMembershipInsert : DbCommandInterceptor
    {
        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default) =>
            command.CommandText.Contains("INSERT INTO workspace_memberships", StringComparison.Ordinal)
                ? throw new InvalidOperationException("Simulated failure after membership insert")
                : ValueTask.FromResult(result);

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default) =>
            command.CommandText.Contains("INSERT INTO workspace_memberships", StringComparison.Ordinal)
                ? throw new InvalidOperationException("Simulated failure after membership insert")
                : ValueTask.FromResult(result);
    }
}
