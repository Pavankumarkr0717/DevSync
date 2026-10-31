namespace DevSync.Domain.Common;

/// <summary>Either a value or one or more field errors. Used for expected validation outcomes.</summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        Errors = [];
    }

    private Result(IReadOnlyList<FieldError> errors)
    {
        if (errors.Count == 0)
        {
            throw new ArgumentException("A failed result needs at least one error.", nameof(errors));
        }

        Errors = errors;
    }

    public bool IsSuccess => Errors.Count == 0;

    public IReadOnlyList<FieldError> Errors { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read the value of a failed result.");

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(IReadOnlyList<FieldError> errors) => new(errors);

    public static Result<T> Failure(string field, string message) => new([new FieldError(field, message)]);
}
