using System.Net;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Health;

public sealed class HealthTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Live_and_ready_are_healthy_when_the_database_is_reachable()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var client = factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Healthy", await ready.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_is_unhealthy_but_live_stays_healthy_when_the_database_is_unreachable()
    {
        await using var factory = DevSyncApiFactory.CreateWithUnreachableDatabase();
        var client = factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync());
    }
}
