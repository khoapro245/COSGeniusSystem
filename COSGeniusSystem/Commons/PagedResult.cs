namespace COSGeniusSystem.Commons;

// Pagination data carried inside ApiResponseModel.Data; not another response envelope.
/// <summary>Canonical pagination payload for new APIs.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
