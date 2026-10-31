using DevSync.Application.Common;
using DevSync.Domain.Common;

namespace DevSync.Application.Workspaces;

/// <summary>Lists the current user's workspaces, paged (US2).</summary>
public sealed class ListMyWorkspacesHandler(IWorkspaceRepository workspaces, ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<WorkspaceSummaryDto>>> HandleAsync(int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var request = PageRequest.Create(page, pageSize);
        if (!request.IsSuccess)
        {
            return Result<PagedResult<WorkspaceSummaryDto>>.Failure(request.Errors);
        }

        var result = await workspaces.ListForMemberAsync(currentUser.UserId, request.Value, cancellationToken);
        return Result<PagedResult<WorkspaceSummaryDto>>.Success(result);
    }
}
