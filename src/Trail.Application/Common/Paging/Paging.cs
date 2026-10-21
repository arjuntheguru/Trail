using Microsoft.EntityFrameworkCore;

namespace Trail.Application.Common.Paging;

/// <summary>Page request from a client. Out-of-range values are clamped rather than rejected.</summary>
public sealed record PageRequest(int Page = 1, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize switch
    {
        < 1 => DefaultPageSize,
        > MaxPageSize => MaxPageSize,
        _ => PageSize,
    };
    public int Skip => (SafePage - 1) * SafePageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
}

public static class PagingExtensions
{
    /// <summary>Counts and pages in the database. The query must already be ordered so pages are stable.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, PageRequest request, CancellationToken ct = default)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip(request.Skip).Take(request.SafePageSize).ToListAsync(ct);
        return new PagedResult<T>(items, request.SafePage, request.SafePageSize, total);
    }
}
