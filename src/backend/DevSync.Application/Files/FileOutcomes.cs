using DevSync.Application.Common;
using DevSync.Domain.Common;
using DevSync.Domain.Files;

namespace DevSync.Application.Files;

internal static class FileOutcomes
{
    /// <summary>A tree-rule failure; invalid names carry the broken rule as the detail message.</summary>
    public static Outcome<T> From<T>(DomainError error) =>
        error.Code == FileErrors.InvalidNameCode
            ? Outcome<T>.Fail(error.Code, error.Message)
            : Outcome<T>.Fail(error.Code);
}
