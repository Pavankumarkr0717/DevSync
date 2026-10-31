using System.Net;
using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Auth;

/// <summary>US1 sign-in: invalid credentials, enumeration resistance, throttling and CSRF (FR-005, FR-008, FR-016, SC-004).</summary>
public sealed class LoginTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Correct_credentials_sign_in_with_any_email_case()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        await factory.CreateAccountAsync(email: "ana@example.com");
        var client = AuthClient.Create(factory);

        var response = await client.LoginAsync(" ANA@Example.COM ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(client.AccessToken);
        Assert.NotNull(client.RefreshToken);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_are_indistinguishable()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        await factory.CreateAccountAsync(email: "ana@example.com");

        var wrongPassword = await AuthClient.Create(factory).LoginAsync("ana@example.com", "wrong password here");
        var unknownEmail = await AuthClient.Create(factory).LoginAsync("nobody@example.com", "wrong password here");

        await wrongPassword.AssertProblemAsync(401, "invalid_credentials");
        await unknownEmail.AssertProblemAsync(401, "invalid_credentials");
        Assert.Equal(await wrongPassword.NormalizedBodyAsync(), await unknownEmail.NormalizedBodyAsync());
        Assert.Null(AuthClient.SetCookie(wrongPassword));
        Assert.Null(AuthClient.SetCookie(unknownEmail));
    }

    [Fact]
    public async Task Empty_password_is_a_validation_error()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);

        var response = await AuthClient.Create(factory).LoginAsync("ana@example.com", "");

        var problem = await response.AssertProblemAsync(400, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Five_failures_block_the_email_for_15_minutes_whether_or_not_it_exists(bool accountExists)
    {
        var clock = DevSyncApiFactory.NewClock();
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres, time: clock);
        if (accountExists)
        {
            await factory.CreateAccountAsync(email: "target@example.com");
        }

        for (var i = 0; i < 5; i++)
        {
            await (await AuthClient.Create(factory).LoginAsync("target@example.com", "wrong password here"))
                .AssertProblemAsync(401, "invalid_credentials");
        }

        await (await AuthClient.Create(factory).LoginAsync("target@example.com", "wrong password here"))
            .AssertProblemAsync(429, "too_many_attempts");
        // Even the correct password is refused while blocked.
        await (await AuthClient.Create(factory).LoginAsync("target@example.com"))
            .AssertProblemAsync(429, "too_many_attempts");

        clock.Advance(TimeSpan.FromMinutes(15));
        var after = await AuthClient.Create(factory).LoginAsync("target@example.com");
        Assert.Equal(accountExists ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task One_client_is_limited_to_20_auth_requests_per_minute()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(
            postgres, settings: new Dictionary<string, string?> { ["DevSync:Auth:IpRateLimitPerMinute"] = "20" });

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 21; i++)
        {
            statuses.Add((await AuthClient.Create(factory).LoginAsync($"user{i}@example.com", "wrong password here")).StatusCode);
        }

        Assert.All(statuses.Take(20), s => Assert.Equal(HttpStatusCode.Unauthorized, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[20]);
    }

    [Fact]
    public async Task Login_without_the_client_header_is_refused()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        await factory.CreateAccountAsync(email: "ana@example.com");

        var response = await AuthClient.Create(factory).SendAsync(HttpMethod.Post, "/api/auth/login",
            new { email = "ana@example.com", password = TestData.DefaultPassword }, clientHeader: false);

        await response.AssertProblemAsync(403, "csrf_check_failed");
    }

    [Fact]
    public async Task Login_from_a_foreign_origin_is_refused()
    {
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres);
        await factory.CreateAccountAsync(email: "ana@example.com");

        var response = await AuthClient.Create(factory).SendAsync(HttpMethod.Post, "/api/auth/login",
            new { email = "ana@example.com", password = TestData.DefaultPassword }, origin: "https://evil.example");
        var sameOrigin = await AuthClient.Create(factory).SendAsync(HttpMethod.Post, "/api/auth/login",
            new { email = "ana@example.com", password = TestData.DefaultPassword }, origin: "https://localhost");

        await response.AssertProblemAsync(403, "csrf_check_failed");
        Assert.Equal(HttpStatusCode.OK, sameOrigin.StatusCode);
    }
}
