namespace SkyJourney.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record PageRequest(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 100;

    public int Skip => (Math.Max(Page, 1) - 1) * Size;

    public int Size => Math.Clamp(PageSize, 1, MaxPageSize);
}
