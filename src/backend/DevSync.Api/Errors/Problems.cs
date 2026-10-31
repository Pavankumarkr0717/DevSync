using DevSync.Application.Common;
using DevSync.Domain.Common;

namespace DevSync.Api.Errors;

/// <summary>Problem Details results used by endpoints.</summary>
public static class Problems
{
    public static IResult Validation(IEnumerable<FieldError> errors)
    {
        var grouped = errors
            .GroupBy(e => e.Field)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());

        return TypedResults.ValidationProblem(
            grouped,
            title: ErrorCatalog.TitleFor(ErrorCodes.ValidationFailed),
            extensions: new Dictionary<string, object?> { ["code"] = ErrorCodes.ValidationFailed });
    }

    public static IResult NotFound(string title = "Workspace not found.") =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = ErrorCodes.NotFound });

    public static IResult Error(string code) =>
        TypedResults.Problem(
            statusCode: ErrorCatalog.StatusFor(code),
            title: ErrorCatalog.TitleFor(code),
            extensions: new Dictionary<string, object?> { ["code"] = code });

    /// <summary>An error with a specific <paramref name="detail"/> message and/or extra members (e.g. <c>currentRevision</c>).</summary>
    public static IResult Error(string code, string? detail, IReadOnlyDictionary<string, object?>? extra = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code };
        foreach (var (key, value) in extra ?? new Dictionary<string, object?>())
        {
            extensions[key] = value;
        }

        return TypedResults.Problem(
            statusCode: ErrorCatalog.StatusFor(code),
            title: ErrorCatalog.TitleFor(code),
            detail: detail,
            extensions: extensions);
    }

    /// <summary>Maps a failed use-case outcome to its Problem Details response.</summary>
    public static IResult From<T>(Outcome<T> outcome) => outcome.Error switch
    {
        null => throw new InvalidOperationException("Outcome succeeded."),
        ErrorCodes.ValidationFailed => Validation(outcome.FieldErrors),
        ErrorCodes.NotFound => NotFound(),
        var code when outcome.Detail is not null || outcome.Extra is not null => Error(code, outcome.Detail, outcome.Extra),
        var code => Error(code),
    };
}
