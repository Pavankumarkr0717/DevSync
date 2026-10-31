using System.Text;
using DevSync.Domain.Common;

namespace DevSync.Domain.Files;

/// <summary>
/// A file or folder name (spec 003 FR-005): 1–255 code points; no <c>/ \ &lt; &gt; : " | ? *</c>, control
/// characters or lone surrogates; no leading or trailing space; no trailing dot; not <c>.</c> or <c>..</c>.
/// Never trimmed or otherwise fixed: what is stored is exactly what was typed.
/// </summary>
public sealed record ItemName
{
    public const string Field = "name";

    private const string ForbiddenCharacters = "/\\<>:\"|?*";

    private ItemName(string value)
    {
        Value = value;
        Normalized = value.ToUpperInvariant();
        var dot = value.LastIndexOf('.');
        Extension = dot >= 0 && dot < value.Length - 1 ? value[(dot + 1)..] : null;
    }

    public string Value { get; }

    /// <summary>Compared for sibling uniqueness (case-insensitive), as Identity normalizes emails.</summary>
    public string Normalized { get; }

    /// <summary>The text after the last dot, if any (<c>.eslintrc.json</c> → <c>json</c>).</summary>
    public string? Extension { get; }

    public static Result<ItemName> Create(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return Fail("Name is required.");
        }

        if (!IsWellFormed(input))
        {
            return Fail("Name contains invalid characters.");
        }

        if (Text.CodePointLength(input) > ProjectLimits.MaxNameLength)
        {
            return Fail($"Name must be at most {ProjectLimits.MaxNameLength} characters.");
        }

        if (input is "." or "..")
        {
            return Fail("Name cannot be \".\" or \"..\".");
        }

        var forbidden = input.IndexOfAny(ForbiddenCharacters.ToCharArray());
        if (forbidden >= 0)
        {
            return Fail($"Name cannot contain \"{input[forbidden]}\". Not allowed: / \\ < > : \" | ? *");
        }

        if (input.Any(c => c < ' ' || c == '\u007F'))
        {
            return Fail("Name cannot contain control characters.");
        }

        if (input[0] == ' ' || input[^1] == ' ')
        {
            return Fail("Name cannot start or end with a space.");
        }

        if (input[^1] == '.')
        {
            return Fail("Name cannot end with a dot.");
        }

        return Result<ItemName>.Success(new ItemName(input));
    }

    public override string ToString() => Value;

    private static Result<ItemName> Fail(string message) => Result<ItemName>.Failure(Field, message);

    // Lone surrogates cannot be stored as UTF-8 text.
    private static bool IsWellFormed(string value)
    {
        var span = value.AsSpan();
        while (!span.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(span, out _, out var consumed) != System.Buffers.OperationStatus.Done)
            {
                return false;
            }

            span = span[consumed..];
        }

        return true;
    }
}
