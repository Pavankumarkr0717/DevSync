using DevSync.Domain.Files;

namespace DevSync.Application.Files;

/// <summary>Storage for workspace trees and file contents (spec 003 research R2–R4).</summary>
public interface IProjectFileRepository
{
    /// <summary>
    /// Locks the workspace against other structure changes and loads its whole tree. The lock and
    /// transaction last until the edit is committed or disposed (disposing without committing
    /// rolls back).
    /// </summary>
    Task<ITreeEdit> BeginTreeEditAsync(Guid workspaceId, CancellationToken cancellationToken);

    /// <summary>Every item of the workspace, without content.</summary>
    Task<IReadOnlyList<ItemDto>> GetTreeAsync(Guid workspaceId, CancellationToken cancellationToken);

    /// <summary>A file's content and revision; null if the item is missing, in another workspace, or a folder.</summary>
    Task<FileContentDto?> GetContentAsync(Guid workspaceId, Guid itemId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores <paramref name="content"/> only if the file is still at <paramref name="expectedRevision"/>,
    /// as one atomic compare-and-swap (FR-019).
    /// </summary>
    Task<SaveContentResult> TrySaveContentAsync(
        Guid workspaceId, Guid itemId, long expectedRevision, string content, int sizeBytes,
        Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>A locked, loaded tree being changed. Changes made through <see cref="Tree"/> are saved by <see cref="CommitAsync"/>.</summary>
public interface ITreeEdit : IAsyncDisposable
{
    ProjectTree Tree { get; }

    void Add(WorkspaceItem item, FileContent? content);

    /// <summary>Deletes the item; the database removes its subtree and contents.</summary>
    void Remove(WorkspaceItem item);

    /// <summary>Saves and commits. Returns <c>false</c> if a sibling-name clash was caught by the database.</summary>
    Task<bool> CommitAsync(CancellationToken cancellationToken);
}

public enum SaveContentStatus
{
    Saved,
    Conflict,
    NotFound,
}

/// <summary>On <see cref="SaveContentStatus.Saved"/>, the new revision; on a conflict, the current one.</summary>
public sealed record SaveContentResult(SaveContentStatus Status, long Revision = 0, DateTimeOffset UpdatedAt = default);
