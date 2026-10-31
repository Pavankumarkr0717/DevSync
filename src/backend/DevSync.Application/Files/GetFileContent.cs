namespace DevSync.Application.Files;

/// <summary>A file's exact content and revision; null when missing, a folder, or in another workspace. Membership is checked by the caller.</summary>
public sealed class GetFileContentHandler(IProjectFileRepository files)
{
    public Task<FileContentDto?> HandleAsync(Guid workspaceId, Guid itemId, CancellationToken cancellationToken) =>
        files.GetContentAsync(workspaceId, itemId, cancellationToken);
}
