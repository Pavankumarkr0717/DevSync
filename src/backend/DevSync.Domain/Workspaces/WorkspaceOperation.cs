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

    /// <summary>Create files and folders, save file content, rename and move (spec 003).</summary>
    EditFiles,

    /// <summary>Delete files and folders with their contents (spec 003).</summary>
    DeleteFiles,

    /// <summary>Create and rename diagrams and save their content (spec 004).</summary>
    EditDiagrams,

    /// <summary>Delete diagrams (spec 004).</summary>
    DeleteDiagrams,
}
