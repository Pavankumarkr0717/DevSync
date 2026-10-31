using DevSync.Domain.Common;

namespace DevSync.Domain.Workspaces;

/// <summary>Business-rule failures for workspaces, members and invitations.</summary>
public static class WorkspaceErrors
{
    public static readonly DomainError CannotChangeOwner = new("cannot_change_owner", "The owner's role cannot be changed.");
    public static readonly DomainError CannotRemoveOwner = new("cannot_remove_owner", "The owner cannot be removed.");
    public static readonly DomainError MemberNotFound = new("member_not_found", "Member not found.");
    public static readonly DomainError AlreadyMember = new("already_member", "That person is already a member.");
    public static readonly DomainError AssignableRoleOnly = new("validation_failed", "Role must be editor or viewer.");
    public static readonly DomainError InvitationNotFound = new("not_found", "Invitation not found.");
}
