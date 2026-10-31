using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevSync.IntegrationTests.Infrastructure;

/// <summary>HTTP helpers and document builders for the spec 004 diagram endpoints.</summary>
public static class DiagramTestData
{
    public static string DiagramsPath(Guid workspaceId) => $"/api/workspaces/{workspaceId}/diagrams";

    public static Task<HttpResponseMessage> CreateDiagramAsync(this HttpClient client, Guid workspaceId, string? name) =>
        client.PostAsJsonAsync(DiagramsPath(workspaceId), new { name });

    public static async Task<Guid> NewDiagramAsync(this HttpClient client, Guid workspaceId, string name)
    {
        var response = await client.CreateDiagramAsync(workspaceId, name);
        Assert.True(response.IsSuccessStatusCode, $"create {name}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    public static Task<HttpResponseMessage> GetDiagramResponseAsync(this HttpClient client, Guid workspaceId, Guid diagramId) =>
        client.GetAsync($"{DiagramsPath(workspaceId)}/{diagramId}");

    public static async Task<JsonElement> GetDiagramAsync(this HttpClient client, Guid workspaceId, Guid diagramId)
    {
        var response = await client.GetDiagramResponseAsync(workspaceId, diagramId);
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync();
    }

    public static Task<HttpResponseMessage> SaveDocumentAsync(
        this HttpClient client, Guid workspaceId, Guid diagramId, long? revision, string json,
        string contentType = "application/json", bool replaceUnsupported = false)
    {
        var url = $"{DiagramsPath(workspaceId)}/{diagramId}/document{(replaceUnsupported ? "?replaceUnsupported=true" : string.Empty)}";
        var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(json)) };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        if (revision is { } r)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{r}\"");
        }

        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> RenameDiagramAsync(this HttpClient client, Guid workspaceId, Guid diagramId, string? name) =>
        client.PostAsJsonAsync($"{DiagramsPath(workspaceId)}/{diagramId}/rename", new { name });

    public static Task<HttpResponseMessage> DeleteDiagramAsync(this HttpClient client, Guid workspaceId, Guid diagramId) =>
        client.DeleteAsync($"{DiagramsPath(workspaceId)}/{diagramId}");

    // ---------- document builders (contracts/diagram-document.v1.schema.json) ----------

    public static JsonObject Node(string id, string type = "microservice", string title = "Orders") => new()
    {
        ["id"] = id,
        ["type"] = type,
        ["title"] = title,
        ["description"] = string.Empty,
        ["metadata"] = new JsonArray(),
        ["position"] = new JsonObject { ["x"] = 0, ["y"] = 0 },
        ["size"] = new JsonObject { ["width"] = 160, ["height"] = 80 },
    };

    public static JsonObject Edge(string id, string source, string target, string label = "") => new()
    {
        ["id"] = id,
        ["source"] = source,
        ["target"] = target,
        ["label"] = label,
    };

    public static JsonObject Document(IEnumerable<JsonObject>? nodes = null, IEnumerable<JsonObject>? edges = null, JsonObject? viewport = null) => new()
    {
        ["schemaVersion"] = 1,
        ["nodes"] = new JsonArray([.. (nodes ?? []).Cast<JsonNode>()]),
        ["edges"] = new JsonArray([.. (edges ?? []).Cast<JsonNode>()]),
        ["viewport"] = viewport,
    };

    /// <summary>Client → API Gateway → Orders → Orders DB, labelled.</summary>
    public static JsonObject SampleDocument() => Document(
        [Node("client", "client", "Web app"), Node("gw", "api_gateway", "Gateway"), Node("orders"), Node("db", "database", "Orders DB")],
        [Edge("e1", "client", "gw", "HTTPS"), Edge("e2", "gw", "orders", "REST"), Edge("e3", "orders", "db", "SQL")],
        new JsonObject { ["x"] = 10, ["y"] = -20, ["zoom"] = 1.5 });
}
