using System.Buffers;
using System.Globalization;
using DevSync.Application.Common;
using DevSync.Domain.Files;
using Microsoft.Net.Http.Headers;

namespace DevSync.Api.Files;

/// <summary>
/// HTTP details of the file-content endpoint (spec 003 research R5, R13): the raw
/// <c>text/plain; charset=utf-8</c> body, read with a hard 1 MiB cap, and revisions as ETags.
/// </summary>
public static class ContentBody
{
    /// <summary>Reads and validates a file-content body. Returns the text, or the error code to answer with.</summary>
    public static async Task<(string? Text, string? Error)> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var (body, error) = await ReadBytesAsync(request, IsPlainUtf8, ProjectLimits.MaxContentBytes, cancellationToken);
        if (error is not null)
        {
            return (null, error);
        }

        var invalid = FileContentRules.Validate(body!, out var text);
        return invalid is null ? (text, null) : (null, invalid.Code);
    }

    /// <summary>
    /// Reads a body of an accepted media type, capped at <paramref name="maxBytes"/> (used by files and
    /// diagrams). Returns the bytes, or <c>unsupported_media_type</c> / <c>content_too_large</c>.
    /// </summary>
    public static async Task<(byte[]? Body, string? Error)> ReadBytesAsync(
        HttpRequest request, Func<string?, bool> acceptsMediaType, int maxBytes, CancellationToken cancellationToken)
    {
        if (!acceptsMediaType(request.ContentType))
        {
            return (null, ErrorCodes.UnsupportedMediaType);
        }

        if (request.ContentLength > maxBytes)
        {
            return (null, ErrorCodes.ContentTooLarge);
        }

        // Read at most one byte past the limit: enough to know the body is too large, without buffering the rest.
        var buffer = ArrayPool<byte>.Shared.Rent(maxBytes + 1);
        try
        {
            var length = 0;
            int read;
            while (length <= maxBytes
                && (read = await request.Body.ReadAsync(buffer.AsMemory(length, maxBytes + 1 - length), cancellationToken)) > 0)
            {
                length += read;
            }

            return length > maxBytes ? (null, ErrorCodes.ContentTooLarge) : (buffer.AsSpan(0, length).ToArray(), null);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary><c>application/json</c>, with no charset or UTF-8.</summary>
    public static bool IsJson(string? contentType) =>
        MediaTypeHeaderValue.TryParse(contentType, out var mediaType)
        && mediaType.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
        && (mediaType.Charset.Length == 0 || mediaType.Charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Parses <c>If-Match: "7"</c>. Missing → <c>revision_required</c>; anything other than one strong,
    /// positive numeric ETag (e.g. <c>*</c>, <c>W/"7"</c>, a list) → <c>malformed_request</c>.
    /// </summary>
    public static (long Revision, string? Error) ParseIfMatch(HttpRequest request)
    {
        var values = request.Headers.IfMatch;
        if (values.Count == 0 || string.IsNullOrWhiteSpace(values.ToString()))
        {
            return (0, ErrorCodes.RevisionRequired);
        }

        if (!EntityTagHeaderValue.TryParseStrictList(values, out var tags)
            || tags.Count != 1
            || tags[0].IsWeak
            || tags[0].Equals(EntityTagHeaderValue.Any))
        {
            return (0, ErrorCodes.MalformedRequest);
        }

        var tag = tags[0].Tag.AsSpan().Trim('"');
        return long.TryParse(tag, NumberStyles.None, CultureInfo.InvariantCulture, out var revision) && revision >= 1
            ? (revision, null)
            : (0, ErrorCodes.MalformedRequest);
    }

    /// <summary>A revision as a strong ETag, e.g. <c>"7"</c>.</summary>
    public static string ETag(long revision) => $"\"{revision.ToString(CultureInfo.InvariantCulture)}\"";

    private static bool IsPlainUtf8(string? contentType) =>
        MediaTypeHeaderValue.TryParse(contentType, out var mediaType)
        && mediaType.MediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase)
        && (mediaType.Charset.Length == 0 || mediaType.Charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase));
}
