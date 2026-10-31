namespace DevSync.Application.Files;

/// <summary>The workspace's whole tree, without content (spec 003 FR-002). Membership is checked by the caller.</summary>
public sealed class GetTreeHandler(IProjectFileRepository files)
{
    public async Task<TreeDto> HandleAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        new(await files.GetTreeAsync(workspaceId, cancellationToken), TreeLimitsDto.Current);
}
