using DevSync.Domain.Common;

namespace DevSync.Domain.Workspaces;

/// <summary>Trimmed; 1–100 code points; no control characters; duplicates allowed.</summary>
public sealed record WorkspaceName
{
    public const int MaxLength = 100;
    public const string Field = "name";

    private WorkspaceName(string value) => Value = value;

    public string Value { get; }

    public static Result<WorkspaceName> Create(string? input)
    {
        var trimmed = input?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result<WorkspaceName>.Failure(Field, "Name is required.");
        }

        if (Text.CodePointLength(trimmed) > MaxLength)
        {
            return Result<WorkspaceName>.Failure(Field, $"Name must be at most {MaxLength} characters.");
        }

        if (Text.ContainsControlCharacters(trimmed))
        {
            return Result<WorkspaceName>.Failure(Field, "Name must not contain control characters.");
        }

        return Result<WorkspaceName>.Success(new WorkspaceName(trimmed));
    }

    public override string ToString() => Value;
}
