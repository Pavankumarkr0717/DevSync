using System.Text;
using DevSync.Application.Common;

namespace DevSync.Application.Files;

/// <summary>
/// Saves a file's whole content if it is still at the revision the editor started from (spec 003
/// FR-018, FR-019). The text has already passed <c>FileContentRules</c>; authorization (EditFiles)
/// is checked by the caller.
/// </summary>
public sealed class SaveFileContentHandler(IProjectFileRepository files, ICurrentUser currentUser, TimeProvider time)
{
    public const string CurrentRevision = "currentRevision";

    public async Task<Outcome<SaveResultDto>> HandleAsync(
        Guid workspaceId, Guid itemId, long expectedRevision, string text, CancellationToken cancellationToken)
    {
        var size = Encoding.UTF8.GetByteCount(text);
        var result = await files.TrySaveContentAsync(
            workspaceId, itemId, expectedRevision, text, size, currentUser.UserId, time.GetUtcNow(), cancellationToken);

        return result.Status switch
        {
            SaveContentStatus.Saved => Outcome<SaveResultDto>.Ok(new SaveResultDto(result.Revision, size, result.UpdatedAt)),
            SaveContentStatus.Conflict => Outcome<SaveResultDto>.Fail(
                ErrorCodes.RevisionConflict, null, new Dictionary<string, object?> { [CurrentRevision] = result.Revision }),
            _ => Outcome<SaveResultDto>.Fail(ErrorCodes.ItemNotFound),
        };
    }
}
