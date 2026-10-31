namespace DevSync.Domain.Workspaces;

/// <summary>Operations that workspace roles are checked against.</summary>
public enum WorkspaceOperation
{
    /// <summary>View details, members and pending invitations count.</summary>
    Read,

    /// <summary>Change name and description.</summary>
    EditDetails,

    /// <summary>See members' email addresses.</summary>
    ViewMemberEmails,

    /// <summary>Invite, revoke invitations, change roles, remove members.</summary>
    ManageMembers,
}
