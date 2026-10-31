using DevSync.IntegrationTests.Infrastructure;

namespace DevSync.IntegrationTests.Security;

/// <summary>File names and content never reach the logs, including on errors (spec 003 NFR-001, constitution VIII).</summary>
public sealed class FileLogScanTests(PostgresFixture postgres)
{
    private const string NameMarker = "secret-marker-NAME";
    private const string ContentMarker = "secret-marker-CONTENT";

    [Fact]
    public async Task File_names_and_content_never_appear_in_logs()
    {
        var logs = new CapturingLoggerProvider();
        await using var factory = await DevSyncApiFactory.CreateAsync(postgres, loggerProvider: logs);
        var team = await factory.TeamWorkspaceAsync();

        var folder = await team.Owner.NewFolderAsync(team.WorkspaceId, null, $"{NameMarker}-dir");
        var file = await team.Owner.NewFileAsync(team.WorkspaceId, folder, $"{NameMarker}.ts");
        await team.Owner.SaveContentAsync(team.WorkspaceId, file, 1, $"const key = '{ContentMarker}';");
        await team.Viewer.GetContentAsync(team.WorkspaceId, file);
        await team.Owner.SaveContentAsync(team.WorkspaceId, file, 1, $"stale {ContentMarker}"); // 412
        await team.Owner.SaveContentBytesAsync(
            team.WorkspaceId, file, 2, [.. System.Text.Encoding.UTF8.GetBytes(ContentMarker), .. new byte[1024 * 1024]]); // 413
        await team.Owner.SaveContentAsync(team.WorkspaceId, file, 2, $"{ContentMarker}\0"); // 400
        await team.Viewer.SaveContentAsync(team.WorkspaceId, file, 2, ContentMarker); // 403
        await team.Owner.CreateItemAsync(team.WorkspaceId, folder, "file", $"{NameMarker}.ts"); // 409
        await team.Owner.CreateItemAsync(team.WorkspaceId, null, "file", $"{NameMarker}/x.ts"); // 400

        Assert.NotEmpty(logs.Messages);
        Assert.DoesNotContain(logs.Messages, m => m.Contains(NameMarker, StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, m => m.Contains(ContentMarker, StringComparison.Ordinal));
    }
}
