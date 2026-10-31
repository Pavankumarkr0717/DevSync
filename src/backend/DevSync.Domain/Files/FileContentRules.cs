using System.Text;
using DevSync.Domain.Common;

namespace DevSync.Domain.Files;

/// <summary>
/// What a file may contain (spec 003 FR-016, FR-017): at most 1 MiB of strict UTF-8 without U+0000.
/// The text is returned exactly as sent: a BOM and every line ending are kept.
/// </summary>
public static class FileContentRules
{
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static DomainError? Validate(ReadOnlySpan<byte> body, out string text)
    {
        text = string.Empty;
        if (body.Length > ProjectLimits.MaxContentBytes)
        {
            return FileErrors.ContentTooLarge;
        }

        string decoded;
        try
        {
            decoded = Strict.GetString(body);
        }
        catch (DecoderFallbackException)
        {
            return FileErrors.InvalidContent;
        }

        if (decoded.Contains('\0', StringComparison.Ordinal))
        {
            return FileErrors.InvalidContent;
        }

        text = decoded;
        return null;
    }
}
