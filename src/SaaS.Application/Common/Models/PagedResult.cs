namespace SaaS.Application.Common.Models;

/// <summary>
/// Standardized pagination envelope returned by collection query endpoints.
/// </summary>
public sealed record PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page number must be at least 1.");
        }

        if (pageSize is < 1 or > PaginationQuery.MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"Page size must be between 1 and {PaginationQuery.MaxPageSize}.");
        }

        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), "Total count cannot be negative.");
        }

        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    public IReadOnlyList<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages { get; }
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1 && TotalPages > 0;
}

/// <summary>
/// Base query parameters enforcing bounded page numbers and sizes.
/// </summary>
public record PaginationQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
    public string? SortBy { get; init; }
    public string SortDirection { get; init; } = "desc";
    public string? Search { get; init; }

    public int NormalizedPage => Page < 1 ? 1 : Page;
    public int NormalizedPageSize => PageSize < 1 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize);
    public bool IsAscending => string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
}
