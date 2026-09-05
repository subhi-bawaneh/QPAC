namespace Dip.Api.Common;

// Standard envelope for paged list endpoints (Drafts now; MIDP/Tracker in Phase 5.6).
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}
