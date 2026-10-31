using System.Net;
using System.Text.Json;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevSync.IntegrationTests.Workspaces;

/// <summary>US2 acceptance scenarios, paging, ordering, restarts and outages.</summary>
public sealed class ListWorkspacesTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AS1_lists_my_workspaces_newest_first_with_my_role()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var client = await factory.ClientForAsync(await factory.CreateAccountAsync());
        var older = await client.CreateWorkspaceAsync("Older");
        var newer = await client.CreateWorkspaceAsync("Newer");

        var page = await GetPageAsync(client, "/api/workspaces");

        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([newer.GetProperty("id").GetGuid(), older.GetProperty("id").GetGuid()], items.Select(i => i.GetProperty("id").GetGuid()));
        Assert.All(items, i => Assert.Equal("owner", i.GetProperty("role").GetString()));
        Assert.Equal("Newer", items[0].GetProperty("name").GetString());
        Assert.True(items[0].TryGetProperty("createdAt", out _));
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(20, page.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task AS2_user_with_no_workspaces_gets_an_empty_page()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);

        var page = await GetPageAsync(await factory.ClientForAsync(await factory.CreateAccountAsync()), "/api/workspaces");

        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task AS3_other_users_workspaces_never_appear()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var a = await factory.ClientForAsync(await factory.CreateAccountAsync("A"));
        var b = await factory.ClientForAsync(await factory.CreateAccountAsync("B"));
        await a.CreateWorkspaceAsync("A1");
        await a.CreateWorkspaceAsync("A2");
        await b.CreateWorkspaceAsync("B1");

        var aNames = Names(await GetPageAsync(a, "/api/workspaces"));
        var bNames = Names(await GetPageAsync(b, "/api/workspaces"));

        Assert.Equal(["A2", "A1"], aNames);
        Assert.Equal(["B1"], bNames);
    }

    [Fact]
    public async Task AS4_request_without_identity_is_rejected()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);

        var response = await factory.CreateClient().GetAsync("/api/workspaces");

        await response.AssertProblemAsync(401, "unauthenticated");
    }

    [Fact]
    public async Task Pages_through_25_workspaces_with_the_default_page_size()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var client = await factory.ClientForAsync(await factory.CreateAccountAsync());
        for (var i = 1; i <= 25; i++)
        {
            await client.CreateWorkspaceAsync($"W{i:00}");
        }

        var first = await GetPageAsync(client, "/api/workspaces");
        var second = await GetPageAsync(client, "/api/workspaces?page=2");
        var beyond = await GetPageAsync(client, "/api/workspaces?page=3");
        var all = await GetPageAsync(client, "/api/workspaces?pageSize=100");

        Assert.Equal(20, first.GetProperty("items").GetArrayLength());
        Assert.Equal(5, second.GetProperty("items").GetArrayLength());
        Assert.Equal(0, beyond.GetProperty("items").GetArrayLength());
        Assert.All([first, second, beyond, all], p => Assert.Equal(25, p.GetProperty("totalCount").GetInt32()));
        Assert.Equal(Names(all), Names(first).Concat(Names(second)));
        Assert.Equal("W25", Names(first)[0]);
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("page=-3", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    public async Task Out_of_range_paging_is_a_validation_error(string query, string field)
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);

        var response = await (await factory.ClientForAsync(await factory.CreateAccountAsync())).GetAsync($"/api/workspaces?{query}");

        var problem = await response.AssertProblemAsync(400, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Theory]
    [InlineData("page=abc")]
    [InlineData("pageSize=1.5")]
    public async Task Non_integer_paging_is_a_malformed_request(string query)
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);

        var response = await (await factory.ClientForAsync(await factory.CreateAccountAsync())).GetAsync($"/api/workspaces?{query}");

        await response.AssertProblemAsync(400, "malformed_request");
    }

    [Fact]
    public async Task Order_is_stable_across_pages_when_creation_times_are_equal()
    {
        var frozen = new FrozenTimeProvider(new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero));
        await using var factory = await DevSyncApiFactory.CreateAsync(
            postgres,
            services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(frozen)));
        var client = await factory.ClientForAsync(await factory.CreateAccountAsync());
        var ids = new List<Guid>();
        for (var i = 0; i < 7; i++)
        {
            ids.Add((await client.CreateWorkspaceAsync($"Same time {i}")).GetProperty("id").GetGuid());
        }

        var paged = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await GetPageAsync(client, $"/api/workspaces?page={page}&pageSize=3");
            paged.AddRange(result.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
        }

        Assert.Equal(7, paged.Distinct().Count());
        Assert.Equal(ids.OrderDescending(new PostgresUuidComparer()), paged);
    }

    [Fact]
    public async Task List_is_identical_after_an_application_restart()
    {
        string connectionString;
        Guid user;
        string before;
        await using (var first = await DevSyncApiFactory.CreateAsync(postgres))
        {
            connectionString = first.ConnectionString;
            user = await first.CreateAccountAsync();
            var client = await first.ClientForAsync(user);
            await client.CreateWorkspaceAsync("One");
            await client.CreateWorkspaceAsync("Two");
            before = (await GetPageAsync(client, "/api/workspaces")).ToString();
        }

        await using var second = new DevSyncApiFactory(connectionString);
        var after = (await GetPageAsync(await second.ClientForAsync(user), "/api/workspaces")).ToString();

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task List_returns_503_when_the_database_is_unreachable()
    {
        await using var factory = DevSyncApiFactory.CreateWithUnreachableDatabase();

        var response = await factory.ClientWithUnverifiableSession().GetAsync("/api/workspaces");

        await response.AssertProblemAsync(503, "service_unavailable");
    }

    private static async Task<JsonElement> GetPageAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    private static List<string?> Names(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()).ToList();

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>PostgreSQL compares uuids bytewise in their textual (big-endian) order.</summary>
    private sealed class PostgresUuidComparer : IComparer<Guid>
    {
        public int Compare(Guid x, Guid y) => string.CompareOrdinal(x.ToString(), y.ToString());
    }
}
