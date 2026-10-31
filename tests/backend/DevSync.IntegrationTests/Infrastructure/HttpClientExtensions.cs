using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevSync.IntegrationTests.Infrastructure;

public static class HttpClientExtensions
{
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Asserts a Problem Details response with the given status and code, and returns its body.</summary>
    public static async Task<JsonElement> AssertProblemAsync(this HttpResponseMessage response, int status, string code)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(status == (int)response.StatusCode, $"Expected {status} {code}, got {(int)response.StatusCode}: {raw}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal(status, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("correlationId").GetString()));
        Assert.Equal(
            response.Headers.GetValues("X-Correlation-ID").Single(),
            body.GetProperty("correlationId").GetString());
        return body;
    }

    /// <summary>A JSON body with the given per-request properties removed, for byte-for-byte comparisons.</summary>
    public static async Task<string> NormalizedBodyAsync(this HttpResponseMessage response, params string[] remove)
    {
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        foreach (var name in remove.Append("correlationId").Append("instance"))
        {
            node.Remove(name);
        }

        return node.ToJsonString();
    }
}
