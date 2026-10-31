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

    /// <summary>Maps a failed use-case outcome to its Problem Details response.</summary>
    public static IResult From<T>(Outcome<T> outcome) => outcome.Error switch
    {
        null => throw new InvalidOperationException("Outcome succeeded."),
        ErrorCodes.ValidationFailed => Validation(outcome.FieldErrors),
        ErrorCodes.NotFound => NotFound(),
        var code => Error(code),
    };
}
