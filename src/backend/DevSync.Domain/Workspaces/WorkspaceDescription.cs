using DevSync.Domain.Common;

namespace DevSync.Domain.Workspaces;

/// <summary>Trimmed; empty → null; ≤ 500 code points; control chars except \n \r \t rejected.</summary>
public sealed record WorkspaceDescription
{
    public const int MaxLength = 500;
    public const string Field = "description";

    private WorkspaceDescription(string value) => Value = value;

    public string Value { get; }

    /// <summary>Returns a successful null result when the input is null, empty or whitespace.</summary>
    public static Result<WorkspaceDescription?> Create(string? input)
    {
        var trimmed = input?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result<WorkspaceDescription?>.Success(null);
        }

        if (Text.CodePointLength(trimmed) > MaxLength)
        {
            return Result<WorkspaceDescription?>.Failure(Field, $"Description must be at most {MaxLength} characters.");
        }

        if (Text.ContainsControlCharacters(trimmed, "\n\r\t"))
        {
            return Result<WorkspaceDescription?>.Failure(Field, "Description must not contain control characters other than line breaks and tabs.");
        }

        return Result<WorkspaceDescription?>.Success(new WorkspaceDescription(trimmed));
    }

    public override string ToString() => Value;
}
