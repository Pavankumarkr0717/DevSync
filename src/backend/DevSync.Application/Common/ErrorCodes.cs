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
}
