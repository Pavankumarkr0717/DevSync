using DevSync.Domain.Identity;
using DevSync.Domain.Workspaces;

namespace DevSync.Infrastructure.Persistence;

/// <summary>Maps <see cref="WorkspaceRole"/> to the lowercase strings stored in the database and used in the API.</summary>
public static class RoleNames
{
    public static string ToDb(WorkspaceRole role) => role switch
    {
        WorkspaceRole.Owner => "owner",
        WorkspaceRole.Editor => "editor",
        WorkspaceRole.Viewer => "viewer",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown workspace role."),
    };

    public static WorkspaceRole FromDb(string value) => value switch
    {
        "owner" => WorkspaceRole.Owner,
        "editor" => WorkspaceRole.Editor,
        "viewer" => WorkspaceRole.Viewer,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown workspace role."),
    };
}

/// <summary>Database strings for other stored enums.</summary>
public static class EnumNames
{
    public static string ToDb(SessionRevocationReason reason) => reason switch
    {
        SessionRevocationReason.Logout => "logout",
        SessionRevocationReason.LogoutAll => "logout_all",
        SessionRevocationReason.RefreshTokenReuse => "refresh_token_reuse",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };

    public static SessionRevocationReason? RevocationFromDb(string value) => value switch
    {
        "logout" => SessionRevocationReason.Logout,
        "logout_all" => SessionRevocationReason.LogoutAll,
        "refresh_token_reuse" => SessionRevocationReason.RefreshTokenReuse,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static string ToDb(InvitationStatus status) => status switch
    {
        InvitationStatus.Pending => "pending",
        InvitationStatus.Accepted => "accepted",
        InvitationStatus.Declined => "declined",
        InvitationStatus.Revoked => "revoked",
        InvitationStatus.Expired => "expired",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static InvitationStatus InvitationStatusFromDb(string value) => value switch
    {
        "pending" => InvitationStatus.Pending,
        "accepted" => InvitationStatus.Accepted,
        "declined" => InvitationStatus.Declined,
        "revoked" => InvitationStatus.Revoked,
        "expired" => InvitationStatus.Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
