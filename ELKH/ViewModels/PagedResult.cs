namespace ELKH.ViewModels
{
    /// <summary>
    /// Generic wrapper for paginated query results.
    /// Returned by service methods that support paging to avoid leaking IQueryable.
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; init; } = new();
        public int TotalCount { get; init; }
        public int TotalPages { get; init; }
    }
}
