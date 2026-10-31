namespace DevSync.Domain.Common;

/// <summary>A validation failure for one input field (field names are camelCase request names).</summary>
public sealed record FieldError(string Field, string Message);
