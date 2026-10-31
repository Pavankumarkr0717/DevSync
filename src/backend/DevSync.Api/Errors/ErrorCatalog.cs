using DevSync.Application.Common;

namespace DevSync.Api.Errors;

/// <summary>HTTP status and human-readable title for every error code in the API contract.</summary>
public static class ErrorCatalog
{
    private static readonly Dictionary<string, (int Status, string Title)> Entries = new()
    {
        [ErrorCodes.ValidationFailed] = (400, "One or more fields are invalid."),
        [ErrorCodes.MalformedRequest] = (400, "The request is malformed."),
        [ErrorCodes.Unauthenticated] = (401, "Sign in to continue."),
        [ErrorCodes.TokenExpired] = (401, "Your access token has expired."),
        [ErrorCodes.SessionEnded] = (401, "Your session has ended. Sign in again."),
        [ErrorCodes.InvalidCredentials] = (401, "Email or password is incorrect."),
        [ErrorCodes.Forbidden] = (403, "You don't have permission to do that."),
        [ErrorCodes.CsrfCheckFailed] = (403, "The request was refused."),
        [ErrorCodes.NotFound] = (404, "Not found."),
        [ErrorCodes.MemberNotFound] = (404, "Member not found."),
        [ErrorCodes.EmailTaken] = (409, "An account with this email already exists."),
        [ErrorCodes.RefreshConflict] = (409, "Session renewal is already in progress. Retry."),
        [ErrorCodes.AlreadyMember] = (409, "That person is already a member."),
        [ErrorCodes.InvitationExists] = (409, "A pending invitation for this email already exists."),
        [ErrorCodes.TooManyInvitations] = (409, "This workspace has too many pending invitations."),
        [ErrorCodes.CannotChangeOwner] = (400, "The owner's role cannot be changed."),
        [ErrorCodes.CannotRemoveOwner] = (400, "The owner cannot be removed."),
        [ErrorCodes.CannotInviteSelf] = (400, "You cannot invite yourself."),
        [ErrorCodes.TooManyAttempts] = (429, "Too many attempts. Try again in a few minutes."),
        [ErrorCodes.ServiceUnavailable] = (503, "The database is unavailable."),
        [ErrorCodes.InternalError] = (500, "An unexpected error occurred."),
    };

    public static int StatusFor(string code) => Entries.TryGetValue(code, out var entry) ? entry.Status : 500;

    public static string TitleFor(string code) => Entries.TryGetValue(code, out var entry) ? entry.Title : "An unexpected error occurred.";

    /// <summary>Default code for a status when none was set explicitly (framework-generated responses).</summary>
    public static string CodeForStatus(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => ErrorCodes.Unauthenticated,
        StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        StatusCodes.Status429TooManyRequests => ErrorCodes.TooManyAttempts,
        StatusCodes.Status503ServiceUnavailable => ErrorCodes.ServiceUnavailable,
        >= 500 => ErrorCodes.InternalError,
        _ => ErrorCodes.MalformedRequest,
    };
}
