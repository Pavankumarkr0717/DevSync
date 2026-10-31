using DevSync.Api.Auth;
using DevSync.Api.Errors;
using DevSync.Application.Common;
using DevSync.Application.Workspaces;
using DevSync.Domain.Workspaces;

namespace DevSync.Api.Endpoints;

public sealed record CreateWorkspaceRequest(string? Name, string? Description);

public sealed record RoleChangeRequest(string? Role);

public sealed record InvitationRequest(string? Email, string? Role);

public static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces").RequireAuthorization();

        group.MapPost(string.Empty, CreateAsync);
        group.MapGet(string.Empty, ListAsync);
        group.MapGet("/{workspaceId}", GetAsync).RequireWorkspace(WorkspaceOperation.Read);
        group.MapPut("/{workspaceId}", UpdateAsync).RequireWorkspace(WorkspaceOperation.EditDetails);

        group.MapPatch("/{workspaceId}/members/{userId}", ChangeRoleAsync).RequireWorkspace(WorkspaceOperation.ManageMembers);
        group.MapDelete("/{workspaceId}/members/{userId}", RemoveMemberAsync).RequireWorkspace(WorkspaceOperation.ManageMembers);

        group.MapGet("/{workspaceId}/invitations", ListInvitationsAsync).RequireWorkspace(WorkspaceOperation.ManageMembers);
        group.MapPost("/{workspaceId}/invitations", InviteAsync).RequireWorkspace(WorkspaceOperation.ManageMembers);
        group.MapDelete("/{workspaceId}/invitations/{invitationId}", RevokeInvitationAsync).RequireWorkspace(WorkspaceOperation.ManageMembers);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateWorkspaceRequest request, CreateWorkspaceHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new CreateWorkspaceCommand(request.Name, request.Description), ct);
        return result.IsSuccess
            ? TypedResults.Created($"/api/workspaces/{result.Value.Id}", result.Value)
            : Problems.Validation(result.Errors);
    }

    private static async Task<IResult> ListAsync(
        int? page, int? pageSize, ListMyWorkspacesHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(page, pageSize, ct);
        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : Problems.Validation(result.Errors);
    }

    // RequireWorkspace has already turned malformed ids, missing workspaces and non-members into the same 404.
    private static async Task<IResult> GetAsync(HttpContext context, GetWorkspaceDetailsHandler handler, CancellationToken ct)
    {
        var access = WorkspaceAuthorization.Access(context);
        var details = await handler.HandleAsync(access.WorkspaceId, access.Role, ct);
        return details is null ? Problems.NotFound() : TypedResults.Ok(details);
    }

    private static async Task<IResult> UpdateAsync(
        HttpContext context, CreateWorkspaceRequest request, UpdateWorkspaceHandler handler, CancellationToken ct)
    {
        var access = WorkspaceAuthorization.Access(context);
        var outcome = await handler.HandleAsync(
            access.WorkspaceId, new CreateWorkspaceCommand(request.Name, request.Description), access.Role, ct);
        return outcome.IsSuccess ? TypedResults.Ok(outcome.Value) : Problems.From(outcome);
    }

    private static async Task<IResult> ChangeRoleAsync(
        HttpContext context, string userId, RoleChangeRequest request, ChangeMemberRoleHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var memberId))
        {
            return Problems.Error(ErrorCodes.MemberNotFound);
        }

        var outcome = await handler.HandleAsync(WorkspaceAuthorization.Access(context).WorkspaceId, memberId, request.Role, ct);
        return outcome.IsSuccess ? TypedResults.Ok(outcome.Value) : Problems.From(outcome);
    }

    private static async Task<IResult> RemoveMemberAsync(
        HttpContext context, string userId, RemoveMemberHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var memberId))
        {
            return Problems.Error(ErrorCodes.MemberNotFound);
        }

        var outcome = await handler.HandleAsync(WorkspaceAuthorization.Access(context).WorkspaceId, memberId, ct);
        return outcome.IsSuccess ? TypedResults.NoContent() : Problems.From(outcome);
    }

    private static async Task<IResult> ListInvitationsAsync(
        HttpContext context, ListWorkspaceInvitationsHandler handler, CancellationToken ct) =>
        TypedResults.Ok(await handler.HandleAsync(WorkspaceAuthorization.Access(context).WorkspaceId, ct));

    private static async Task<IResult> InviteAsync(
        HttpContext context, InvitationRequest request, InviteMemberHandler handler, CancellationToken ct)
    {
        var workspaceId = WorkspaceAuthorization.Access(context).WorkspaceId;
        var outcome = await handler.HandleAsync(workspaceId, new InviteMemberCommand(request.Email, request.Role), ct);
        return outcome.IsSuccess
            ? TypedResults.Created($"/api/workspaces/{workspaceId}/invitations/{outcome.Value.Id}", outcome.Value)
            : Problems.From(outcome);
    }

    private static async Task<IResult> RevokeInvitationAsync(
        HttpContext context, string invitationId, RevokeInvitationHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParse(invitationId, out var id))
        {
            return Problems.NotFound("Invitation not found.");
        }

        var outcome = await handler.HandleAsync(WorkspaceAuthorization.Access(context).WorkspaceId, id, ct);
        return outcome.IsSuccess ? TypedResults.NoContent() : Problems.NotFound("Invitation not found.");
    }
}
