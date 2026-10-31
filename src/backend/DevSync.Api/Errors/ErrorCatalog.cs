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
        [ErrorCodes.ItemNotFound] = (404, "File or folder not found."),
        [ErrorCodes.ParentNotFound] = (404, "The target folder was not found."),
        [ErrorCodes.InvalidName] = (400, "The name is not allowed."),
        [ErrorCodes.UnsupportedExtension] = (400, "Supported: .cs .js .ts .tsx .jsx .json .html .css .py .java .md"),
        [ErrorCodes.TooDeep] = (400, "Folders can be nested at most 20 levels."),
        [ErrorCodes.PathTooLong] = (400, "The full path would be longer than 1,024 characters."),
        [ErrorCodes.InvalidMove] = (400, "A folder cannot be moved into itself or one of its folders."),
        [ErrorCodes.InvalidContent] = (400, "The file contains characters that can't be saved."),
        [ErrorCodes.NameTaken] = (409, "An item with this name already exists here."),
        [ErrorCodes.TooManyItems] = (409, "This workspace has reached 5,000 files and folders."),
        [ErrorCodes.RevisionConflict] = (412, "Someone else saved a newer version."),
        [ErrorCodes.ContentTooLarge] = (413, "The file is larger than 1 MiB."),
        [ErrorCodes.UnsupportedMediaType] = (415, "File content must be sent as text/plain; charset=utf-8."),
        [ErrorCodes.RevisionRequired] = (428, "State the revision you edited in an If-Match header."),
        [ErrorCodes.DiagramNotFound] = (404, "Diagram not found."),
        [ErrorCodes.DiagramNameTaken] = (409, "A diagram with this name already exists."),
        [ErrorCodes.TooManyDiagrams] = (409, "This workspace has reached 50 diagrams."),
        [ErrorCodes.InvalidDiagram] = (400, "The diagram couldn't be saved."),
        [ErrorCodes.UnsupportedSchemaVersion] = (400, "This diagram format version is not supported."),
        [ErrorCodes.StoredVersionUnsupported] = (409, "This diagram was saved by a newer version of DevSync."),
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
