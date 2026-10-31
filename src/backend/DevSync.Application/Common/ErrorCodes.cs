namespace DevSync.Application.Common;

/// <summary>Stable machine-readable error codes returned to clients in Problem Details <c>code</c>.</summary>
public static class ErrorCodes
{
    // 001
    public const string ValidationFailed = "validation_failed";
    public const string MalformedRequest = "malformed_request";
    public const string Unauthenticated = "unauthenticated";
    public const string NotFound = "not_found";
    public const string ServiceUnavailable = "service_unavailable";
    public const string InternalError = "internal_error";

    // 002 — authentication and sessions
    public const string TokenExpired = "token_expired";
    public const string SessionEnded = "session_ended";
    public const string InvalidCredentials = "invalid_credentials";
    public const string EmailTaken = "email_taken";
    public const string TooManyAttempts = "too_many_attempts";
    public const string CsrfCheckFailed = "csrf_check_failed";
    public const string RefreshConflict = "refresh_conflict";

    // 002 — authorization, members and invitations
    public const string Forbidden = "forbidden";
    public const string CannotChangeOwner = "cannot_change_owner";
    public const string CannotRemoveOwner = "cannot_remove_owner";
    public const string MemberNotFound = "member_not_found";
    public const string AlreadyMember = "already_member";
    public const string CannotInviteSelf = "cannot_invite_self";
    public const string InvitationExists = "invitation_exists";
    public const string TooManyInvitations = "too_many_invitations";

    // 003 — project files
    public const string ItemNotFound = "item_not_found";
    public const string ParentNotFound = "parent_not_found";
    public const string InvalidName = "invalid_name";
    public const string UnsupportedExtension = "unsupported_extension";
    public const string TooDeep = "too_deep";
    public const string PathTooLong = "path_too_long";
    public const string InvalidMove = "invalid_move";
    public const string InvalidContent = "invalid_content";
    public const string NameTaken = "name_taken";
    public const string TooManyItems = "too_many_items";
    public const string RevisionConflict = "revision_conflict";
    public const string ContentTooLarge = "content_too_large";
    public const string UnsupportedMediaType = "unsupported_media_type";
    public const string RevisionRequired = "revision_required";

    // 004 — diagrams
    public const string DiagramNotFound = "diagram_not_found";
    public const string DiagramNameTaken = "diagram_name_taken";
    public const string TooManyDiagrams = "too_many_diagrams";
    public const string InvalidDiagram = "invalid_diagram";
    public const string UnsupportedSchemaVersion = "unsupported_schema_version";
    public const string StoredVersionUnsupported = "stored_version_unsupported";
}
