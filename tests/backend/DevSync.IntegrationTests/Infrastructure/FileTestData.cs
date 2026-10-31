using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DevSync.Domain.Files;
using DevSync.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace DevSync.IntegrationTests.Infrastructure;

/// <summary>A workspace with one account per role, plus a signed-in non-member and an anonymous client.</summary>
public sealed record TeamWorkspace(
    Guid WorkspaceId,
    Guid OwnerId,
    Guid EditorId,
    Guid ViewerId,
    HttpClient Owner,
    HttpClient Editor,
    HttpClient Viewer,
    HttpClient NonMember,
    HttpClient Anonymous)
{
    public string Base => $"/api/workspaces/{WorkspaceId}";
}

/// <summary>HTTP helpers for the spec 003 file endpoints (contracts/openapi.yaml).</summary>
public static class FileTestData
{
    public const string TextPlain = "text/plain; charset=utf-8";

    public static async Task<TeamWorkspace> TeamWorkspaceAsync(this DevSyncApiFactory factory)
    {
        var ownerId = await factory.CreateAccountAsync("Owner");
        var editorId = await factory.CreateAccountAsync("Editor");
        var viewerId = await factory.CreateAccountAsync("Viewer");
        var outsiderId = await factory.CreateAccountAsync("Outsider");

        var owner = await factory.ClientForAsync(ownerId);
        var workspaceId = (await owner.CreateWorkspaceAsync("Files")).GetProperty("id").GetGuid();
        await factory.AddMemberAsync(workspaceId, editorId, WorkspaceRole.Editor);
        await factory.AddMemberAsync(workspaceId, viewerId, WorkspaceRole.Viewer);

        return new TeamWorkspace(
            workspaceId,
            ownerId,
            editorId,
            viewerId,
            owner,
            await factory.ClientForAsync(editorId),
            await factory.ClientForAsync(viewerId),
            await factory.ClientForAsync(outsiderId),
            factory.CreateClient());
    }

    public static Task<HttpResponseMessage> CreateItemAsync(this HttpClient client, Guid workspaceId, Guid? parentId, string kind, string name) =>
        client.PostAsJsonAsync($"/api/workspaces/{workspaceId}/items", new { parentId, kind, name });

    /// <summary>Creates an item and returns its id, failing the test if creation fails.</summary>
    public static async Task<Guid> NewItemAsync(this HttpClient client, Guid workspaceId, Guid? parentId, string kind, string name)
    {
        var response = await client.CreateItemAsync(workspaceId, parentId, kind, name);
        Assert.True(response.IsSuccessStatusCode, $"create {name}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    public static Task<Guid> NewFolderAsync(this HttpClient client, Guid workspaceId, Guid? parentId, string name) =>
        client.NewItemAsync(workspaceId, parentId, "folder", name);

    public static Task<Guid> NewFileAsync(this HttpClient client, Guid workspaceId, Guid? parentId, string name) =>
        client.NewItemAsync(workspaceId, parentId, "file", name);

    public static async Task<JsonElement> GetTreeAsync(this HttpClient client, Guid workspaceId)
    {
        var response = await client.GetAsync($"/api/workspaces/{workspaceId}/tree");
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync();
    }

    /// <summary>The items of the tree, by id.</summary>
    public static async Task<Dictionary<Guid, JsonElement>> GetItemsAsync(this HttpClient client, Guid workspaceId) =>
        (await client.GetTreeAsync(workspaceId)).GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("id").GetGuid());

    public static Task<HttpResponseMessage> GetContentResponseAsync(this HttpClient client, Guid workspaceId, Guid itemId) =>
        client.GetAsync($"/api/workspaces/{workspaceId}/files/{itemId}/content");

    /// <summary>The file's exact bytes as text and its revision from the ETag.</summary>
    public static async Task<(string Text, long Revision)> GetContentAsync(this HttpClient client, Guid workspaceId, Guid itemId)
    {
        var response = await client.GetContentResponseAsync(workspaceId, itemId);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return (Encoding.UTF8.GetString(bytes), long.Parse(response.Headers.ETag!.Tag.Trim('"'), System.Globalization.CultureInfo.InvariantCulture));
    }

    public static Task<HttpResponseMessage> SaveContentAsync(
        this HttpClient client, Guid workspaceId, Guid itemId, long? revision, string text, string contentType = TextPlain) =>
        client.SaveContentBytesAsync(workspaceId, itemId, revision, Encoding.UTF8.GetBytes(text), contentType);

    public static Task<HttpResponseMessage> SaveContentBytesAsync(
        this HttpClient client, Guid workspaceId, Guid itemId, long? revision, byte[] body, string contentType = TextPlain, string? ifMatch = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/workspaces/{workspaceId}/files/{itemId}/content")
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }
        else if (revision is { } r)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{r}\"");
        }

        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> RenameAsync(this HttpClient client, Guid workspaceId, Guid itemId, string name) =>
        client.PostAsJsonAsync($"/api/workspaces/{workspaceId}/items/{itemId}/rename", new { name });

    public static Task<HttpResponseMessage> MoveAsync(this HttpClient client, Guid workspaceId, Guid itemId, Guid? parentId) =>
        client.PostAsJsonAsync($"/api/workspaces/{workspaceId}/items/{itemId}/move", new { parentId });

    public static Task<HttpResponseMessage> DeleteItemAsync(this HttpClient client, Guid workspaceId, Guid itemId) =>
        client.DeleteAsync($"/api/workspaces/{workspaceId}/items/{itemId}");

    /// <summary>Inserts <paramref name="count"/> top-level folders directly (for limit tests).</summary>
    public static async Task SeedTopLevelFoldersAsync(this DevSyncApiFactory factory, Guid workspaceId, int count)
    {
        await using var scope = factory.CreateDbScope();
        await scope.Db.Database.ExecuteSqlAsync($"""
            INSERT INTO workspace_items (id, workspace_id, parent_id, kind, name, normalized_name, created_at, updated_at)
            SELECT gen_random_uuid(), {workspaceId}, NULL, 'folder', 'seed' || n, 'SEED' || n, now(), now()
            FROM generate_series(1, {count}) AS n
            """);
    }

    /// <summary>
    /// Checks the stored tree directly: every parent is a folder of the same workspace, no cycles,
    /// sibling names unique ignoring case, depth and count within limits, and a content row for exactly every file.
    /// </summary>
    public static async Task AssertTreeInvariantsAsync(this DevSyncApiFactory factory, Guid workspaceId)
    {
        await using var scope = factory.CreateDbScope();
        var items = await scope.Db.WorkspaceItems.AsNoTracking().Where(i => i.WorkspaceId == workspaceId).ToListAsync();
        var contentIds = await (
                from c in scope.Db.FileContents.AsNoTracking()
                join i in scope.Db.WorkspaceItems.AsNoTracking() on c.ItemId equals i.Id
                where i.WorkspaceId == workspaceId
                select c.ItemId)
            .ToListAsync();
        var byId = items.ToDictionary(i => i.Id);

        Assert.True(items.Count <= ProjectLimits.MaxItemsPerWorkspace);
        foreach (var item in items)
        {
            var seen = new HashSet<Guid> { item.Id };
            var depth = 0;
            for (var parent = item.ParentId; parent is { } p; parent = byId[p].ParentId)
            {
                Assert.True(byId.TryGetValue(p, out var parentItem), "parent outside the workspace");
                Assert.Equal(ItemKind.Folder, parentItem.Kind);
                Assert.True(seen.Add(p), "cycle");
                depth++;
            }

            Assert.True(depth <= ProjectLimits.MaxDepth);
        }

        Assert.DoesNotContain(items.GroupBy(i => (i.ParentId, i.NormalizedName)), g => g.Count() > 1);
        Assert.Equal(items.Where(i => i.Kind == ItemKind.File).Select(i => i.Id).Order(), contentIds.Order());
    }
}
