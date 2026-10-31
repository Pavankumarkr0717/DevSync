using DevSync.Domain.Common;

namespace DevSync.Application.Common;

/// <summary>
/// The result of a use case: a value, or an error code (see <see cref="ErrorCodes"/>) with
/// field errors when the code is <c>validation_failed</c>. A failure may carry a specific
/// <see cref="Detail"/> message and <see cref="Extra"/> members for the client.
/// </summary>
public sealed class Outcome<T>
{
    private readonly T? _value;

    private Outcome(
        T? value, string? error, IReadOnlyList<FieldError> fieldErrors,
        string? detail = null, IReadOnlyDictionary<string, object?>? extra = null)
    {
        _value = value;
        Error = error;
        FieldErrors = fieldErrors;
        Detail = detail;
        Extra = extra;
    }

    public string? Error { get; }

    public IReadOnlyList<FieldError> FieldErrors { get; }

    /// <summary>A message more specific than the code's title (e.g. which name rule was broken).</summary>
    public string? Detail { get; }

    /// <summary>Extra machine-readable members for the client (e.g. <c>currentRevision</c> on a conflict).</summary>
    public IReadOnlyDictionary<string, object?>? Extra { get; }

    public bool IsSuccess => Error is null;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"Outcome failed with {Error}.");

    public static Outcome<T> Ok(T value) => new(value, null, []);

    public static Outcome<T> Fail(string code) => new(default, code, []);

    public static Outcome<T> Fail(string code, string? detail, IReadOnlyDictionary<string, object?>? extra = null) =>
        new(default, code, [], detail, extra);

    public static Outcome<T> Invalid(IReadOnlyList<FieldError> errors) => new(default, ErrorCodes.ValidationFailed, errors);

    public static Outcome<T> Invalid(string field, string message) => Invalid([new FieldError(field, message)]);

    public Outcome<TOther> Cast<TOther>() =>
        IsSuccess
            ? throw new InvalidOperationException("Only failures can be cast.")
            : new Outcome<TOther>(default, Error, FieldErrors, Detail, Extra);

    /// <summary>Maps a domain rule failure; <c>validation_failed</c> rule errors become a field error on <paramref name="field"/>.</summary>
    public static Outcome<T> From(DomainError error, string field = "role") =>
        error.Code == ErrorCodes.ValidationFailed ? Invalid(field, error.Message) : Fail(error.Code);
}
