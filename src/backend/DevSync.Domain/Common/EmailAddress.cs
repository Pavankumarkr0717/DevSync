using System.Text;

namespace DevSync.Domain.Common;

/// <summary>
/// Email rules shared by accounts and invitations: trimmed, at most 254 characters, a pragmatic
/// syntax check, compared case-insensitively. Normalization matches ASP.NET Core Identity
/// (Unicode FormC, then upper-invariant) so invitations match account emails exactly.
/// </summary>
public static class EmailAddress
{
    public const int MaxLength = 254;
    public const string Field = "email";

    public static Result<string> Create(string? input)
    {
        var trimmed = input?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result<string>.Failure(Field, "Email is required.");
        }

        if (trimmed.Length > MaxLength)
        {
            return Result<string>.Failure(Field, $"Email must be at most {MaxLength} characters.");
        }

        if (!LooksValid(trimmed))
        {
            return Result<string>.Failure(Field, "Enter a valid email address.");
        }

        return Result<string>.Success(trimmed);
    }

    public static string Normalize(string email) =>
        email.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();

    private static bool LooksValid(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
        {
            return false;
        }

        var domain = email[(at + 1)..];
        return !email.Any(char.IsWhiteSpace)
            && !email.Any(char.IsControl)
            && domain.Contains('.')
            && !domain.StartsWith('.')
            && !domain.EndsWith('.');
    }
}
