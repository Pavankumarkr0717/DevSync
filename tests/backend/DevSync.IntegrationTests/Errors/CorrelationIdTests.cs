using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Errors;

public sealed class CorrelationIdTests(PostgresFixture postgres)
{
    private const string Header = "X-Correlation-ID";

    [Fact]
    public async Task A_valid_inbound_correlation_id_is_echoed()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(Header, "test-correlation-123");

        var response = await client.GetAsync("/health/live");

        Assert.Equal("test-correlation-123", response.Headers.GetValues(Header).Single());
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    [InlineData("<script>")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 65 chars
    public async Task An_invalid_inbound_correlation_id_is_replaced(string inbound)
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(Header, inbound);

        var response = await client.GetAsync("/health/live");

        var echoed = response.Headers.GetValues(Header).Single();
        Assert.NotEqual(inbound, echoed);
        Assert.Matches("^[A-Za-z0-9-]{1,64}$", echoed);
    }

    [Fact]
    public async Task Error_responses_carry_the_same_correlation_id_as_the_header()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(Header, "error-correlation-1");

        var unauthenticated = await client.GetAsync("/api/workspaces");
        var body = await unauthenticated.AssertProblemAsync(401, "unauthenticated");
        Assert.Equal("error-correlation-1", body.GetProperty("correlationId").GetString());

        // Unknown routes are 401 for anonymous callers (fallback policy) and 404 once signed in.
        var anonymousUnknown = await client.GetAsync("/api/does-not-exist");
        await anonymousUnknown.AssertProblemAsync(401, "unauthenticated");

        var signedIn = await factory.ClientForAsync(await factory.CreateAccountAsync());
        signedIn.DefaultRequestHeaders.Add(Header, "error-correlation-2");
        var unknownRoute = await signedIn.GetAsync("/api/does-not-exist");
        var notFound = await unknownRoute.AssertProblemAsync(404, "not_found");
        Assert.Equal("error-correlation-2", notFound.GetProperty("correlationId").GetString());
    }
}
