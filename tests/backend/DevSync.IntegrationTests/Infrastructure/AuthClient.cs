using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DevSync.IntegrationTests.Infrastructure;

/// <summary>
/// Drives the real auth endpoints like the SPA does: sends <c>X-DevSync-Client: web</c>, keeps the
/// access token in memory and the refresh cookie value explicitly (so tests can replay old values).
/// </summary>
public sealed class AuthClient(HttpClient http)
{
    public const string CookieName = "devsync_rt";

    public HttpClient Http { get; } = http;

    public string? AccessToken { get; private set; }

    public string? RefreshToken { get; set; }

    public List<string> ResponseBodies { get; } = [];

    public static AuthClient Create(DevSyncApiFactory factory) =>
        new(factory.CreateDefaultClient(new Uri("https://localhost")));

    public Task<HttpResponseMessage> RegisterAsync(string email, string password = TestData.DefaultPassword, string displayName = "Test User") =>
        SendAsync(HttpMethod.Post, "/api/auth/register", new { displayName, email, password });

    public Task<HttpResponseMessage> LoginAsync(string email, string password = TestData.DefaultPassword) =>
        SendAsync(HttpMethod.Post, "/api/auth/login", new { email, password });

    public Task<HttpResponseMessage> RefreshAsync(string? cookie = null) =>
        SendAsync(HttpMethod.Post, "/api/auth/refresh", cookie: cookie ?? RefreshToken);

    public Task<HttpResponseMessage> LogoutAsync() => SendAsync(HttpMethod.Post, "/api/auth/logout");

    public Task<HttpResponseMessage> LogoutAllAsync() => SendAsync(HttpMethod.Post, "/api/auth/logout-all");

    public Task<HttpResponseMessage> GetAsync(string path) => SendAsync(HttpMethod.Get, path);

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, object? body = null, string? cookie = null, bool clientHeader = true, string? origin = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (clientHeader)
        {
            request.Headers.Add("X-DevSync-Client", "web");
        }

        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        var cookieValue = cookie ?? RefreshToken;
        if (cookieValue is not null && path.StartsWith("/api/auth/", StringComparison.Ordinal))
        {
            request.Headers.Add("Cookie", $"{CookieName}={cookieValue}");
        }

        if (AccessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        }

        var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        ResponseBodies.Add(text);

        if (SetCookie(response) is { } setCookie)
        {
            RefreshToken = setCookie.Value.Length == 0 ? null : setCookie.Value;
        }

        if (response.IsSuccessStatusCode && text.Contains("\"accessToken\"", StringComparison.Ordinal))
        {
            AccessToken = JsonDocument.Parse(text).RootElement.GetProperty("accessToken").GetString();
        }

        return response;
    }

    /// <summary>The refresh cookie set by a response: its value and raw attributes.</summary>
    public static (string Value, string Raw)? SetCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        var raw = values.FirstOrDefault(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal));
        if (raw is null)
        {
            return null;
        }

        var value = raw[(CookieName.Length + 1)..].Split(';')[0];
        return (value, raw);
    }

    /// <summary>A plain client that sends the current access token (for non-auth API calls).</summary>
    public HttpClient BearerClient(DevSyncApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        return client;
    }
}
