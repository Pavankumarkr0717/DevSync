namespace DevSync.Domain.Common;

/// <summary>A business-rule failure with a stable machine-readable code (e.g. <c>cannot_remove_owner</c>).</summary>
public sealed record DomainError(string Code, string Message);
