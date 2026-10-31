using DevSync.Domain.Common;

namespace DevSync.Application.Common;

/// <summary>A validated page request: page ≥ 1, 1 ≤ pageSize ≤ 100 (default 1 and 20).</summary>
public sealed record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private PageRequest(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    public int Page { get; }

    public int PageSize { get; }

    public int Offset => (Page - 1) * PageSize;

    public static Result<PageRequest> Create(int? page, int? pageSize)
    {
        var errors = new List<FieldError>();
        var p = page ?? 1;
        var size = pageSize ?? DefaultPageSize;

        if (p < 1)
        {
            errors.Add(new FieldError("page", "Page must be 1 or greater."));
        }

        if (size is < 1 or > MaxPageSize)
        {
            errors.Add(new FieldError("pageSize", $"Page size must be between 1 and {MaxPageSize}."));
        }

        return errors.Count > 0 ? Result<PageRequest>.Failure(errors) : Result<PageRequest>.Success(new PageRequest(p, size));
    }
}
