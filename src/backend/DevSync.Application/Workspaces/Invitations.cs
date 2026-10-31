using DevSync.Application.Common;
using DevSync.Application.Identity;
using DevSync.Domain.Common;
using DevSync.Domain.Workspaces;
using Microsoft.Extensions.Logging;

namespace DevSync.Application.Workspaces;

public sealed record InviteMemberCommand(string? Email, string? Role);

/// <summary>
/// Owner invites an email address (spec 002 FR-020). The outcome never reveals whether an
/// account exists for that email.
/// </summary>
public sealed class InviteMemberHandler(
    IWorkspaceRepository workspaces,
    IInvitationRepository invitations,
    IUserDirectory users,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<InviteMemberHandler> logger)
{
    public const int MaxPendingPerWorkspace = 50;

    public async Task<Outcome<WorkspaceInvitationDto>> HandleAsync(
        Guid workspaceId, InviteMemberCommand command, CancellationToken cancellationToken)
    {
        var role = AssignableRoles.Parse(command.Role);
        var created = Invitation.Create(workspaceId, command.Email, role ?? WorkspaceRole.Editor, currentUser.UserId, time);
        var errors = created.Errors.ToList();
        if (role is null)
        {
            errors.Add(new FieldError(AssignableRoles.Field, AssignableRoles.Message));
        }

        if (errors.Count > 0)
        {
            return Outcome<WorkspaceInvitationDto>.Invalid(errors);
        }

        var invitation = created.Value;
        if (await users.GetNormalizedEmailAsync(currentUser.UserId, cancellationToken) == invitation.NormalizedEmail)
        {
            return Outcome<WorkspaceInvitationDto>.Fail(ErrorCodes.CannotInviteSelf);
        }

        if (await workspaces.IsMemberByEmailAsync(workspaceId, invitation.NormalizedEmail, cancellationToken))
        {
            return Outcome<WorkspaceInvitationDto>.Fail(ErrorCodes.AlreadyMember);
        }

        await invitations.ExpireLapsedAsync(workspaceId, time.GetUtcNow(), cancellationToken);
        if (await invitations.HasPendingAsync(workspaceId, invitation.NormalizedEmail, cancellationToken))
        {
            return Outcome<WorkspaceInvitationDto>.Fail(ErrorCodes.InvitationExists);
        }

        if (await invitations.CountPendingAsync(workspaceId, cancellationToken) >= MaxPendingPerWorkspace)
        {
            return Outcome<WorkspaceInvitationDto>.Fail(ErrorCodes.TooManyInvitations);
        }

        if (!await invitations.TryAddAsync(invitation, cancellationToken))
        {
            return Outcome<WorkspaceInvitationDto>.Fail(ErrorCodes.InvitationExists);
        }

        logger.LogInformation(
            "Security event {Event} in workspace {WorkspaceId}: invitation {InvitationId} as {Role} (by {UserId})",
            "membership.invited", workspaceId, invitation.Id, invitation.Role, currentUser.UserId);

        var inviter = await users.GetAsync(currentUser.UserId, cancellationToken)
            ?? throw new InvalidOperationException("The authenticated user no longer exists.");
        return Outcome<WorkspaceInvitationDto>.Ok(new WorkspaceInvitationDto(
            invitation.Id, invitation.Email, invitation.Role, inviter, invitation.CreatedAt, invitation.ExpiresAt));
    }
}

/// <summary>Owner revokes a pending invitation.</summary>
public sealed class RevokeInvitationHandler(
    IInvitationRepository invitations,
    IWorkspaceRepository workspaces,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<RevokeInvitationHandler> logger)
{
    public async Task<Outcome<bool>> HandleAsync(Guid workspaceId, Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await invitations.FindAsync(invitationId, cancellationToken);
        if (invitation is null || invitation.WorkspaceId != workspaceId)
        {
            return Outcome<bool>.Fail(ErrorCodes.NotFound);
        }

        var error = invitation.Revoke(time.GetUtcNow());
        await workspaces.SaveChangesAsync(cancellationToken); // also persists a lapsed invitation as expired
        if (error is not null)
        {
            return Outcome<bool>.Fail(ErrorCodes.NotFound);
        }

        logger.LogInformation(
            "Security event {Event} in workspace {WorkspaceId}: invitation {InvitationId} (by {UserId})",
            "membership.invitation_revoked", workspaceId, invitationId, currentUser.UserId);
        return Outcome<bool>.Ok(true);
    }
}

/// <summary>Owner lists the workspace's open invitations.</summary>
public sealed class ListWorkspaceInvitationsHandler(IInvitationRepository invitations, TimeProvider time)
{
    public async Task<IReadOnlyList<WorkspaceInvitationDto>> HandleAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await invitations.ExpireLapsedAsync(workspaceId, now, cancellationToken);
        return await invitations.ListOpenForWorkspaceAsync(workspaceId, now, cancellationToken);
    }
}

/// <summary>Open invitations addressed to the current user's email.</summary>
public sealed class ListMyInvitationsHandler(
    IInvitationRepository invitations,
    IUserDirectory users,
    ICurrentUser currentUser,
    TimeProvider time)
{
    public async Task<IReadOnlyList<ReceivedInvitationDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var email = await users.GetNormalizedEmailAsync(currentUser.UserId, cancellationToken);
        return email is null ? [] : await invitations.ListOpenForEmailAsync(email, time.GetUtcNow(), cancellationToken);
    }
}

/// <summary>Accepts an invitation: membership and invitation status change together (one transaction).</summary>
public sealed class AcceptInvitationHandler(
    IInvitationRepository invitations,
    IWorkspaceRepository workspaces,
    IUserDirectory users,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<AcceptInvitationHandler> logger)
{
    public async Task<Outcome<WorkspaceSummaryDto>> HandleAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await invitations.FindAsync(invitationId, cancellationToken);
        var email = await users.GetNormalizedEmailAsync(currentUser.UserId, cancellationToken);
        if (invitation is null || email is null)
        {
            return Outcome<WorkspaceSummaryDto>.Fail(ErrorCodes.NotFound);
        }

        var now = time.GetUtcNow();
        if (invitation.Accept(currentUser.UserId, email, now) is not null)
        {
            await workspaces.SaveChangesAsync(cancellationToken); // persist a lapsed invitation as expired
            return Outcome<WorkspaceSummaryDto>.Fail(ErrorCodes.NotFound);
        }

        var workspace = await workspaces.GetAsync(invitation.WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            return Outcome<WorkspaceSummaryDto>.Fail(ErrorCodes.NotFound);
        }

        if (workspace.AddMember(currentUser.UserId, invitation.Role, now) is { } error)
        {
            return Outcome<WorkspaceSummaryDto>.From(error);
        }

        await workspaces.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Security event {Event} in workspace {WorkspaceId}: user {UserId} joined as {Role} via invitation {InvitationId}",
            "membership.joined", workspace.Id, currentUser.UserId, invitation.Role, invitation.Id);
        return Outcome<WorkspaceSummaryDto>.Ok(new WorkspaceSummaryDto(workspace.Id, workspace.Name, invitation.Role, workspace.CreatedAt));
    }
}

/// <summary>Declines an invitation addressed to the current user.</summary>
public sealed class DeclineInvitationHandler(
    IInvitationRepository invitations,
    IWorkspaceRepository workspaces,
    IUserDirectory users,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<DeclineInvitationHandler> logger)
{
    public async Task<Outcome<bool>> HandleAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await invitations.FindAsync(invitationId, cancellationToken);
        var email = await users.GetNormalizedEmailAsync(currentUser.UserId, cancellationToken);
        if (invitation is null || email is null)
        {
            return Outcome<bool>.Fail(ErrorCodes.NotFound);
        }

        var error = invitation.Decline(email, time.GetUtcNow());
        await workspaces.SaveChangesAsync(cancellationToken);
        if (error is not null)
        {
            return Outcome<bool>.Fail(ErrorCodes.NotFound);
        }

        logger.LogInformation(
            "Security event {Event} in workspace {WorkspaceId}: invitation {InvitationId} declined by {UserId}",
            "membership.declined", invitation.WorkspaceId, invitation.Id, currentUser.UserId);
        return Outcome<bool>.Ok(true);
    }
}
