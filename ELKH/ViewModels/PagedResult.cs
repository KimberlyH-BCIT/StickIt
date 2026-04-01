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
        public int Page { get; init; }
        public int PageSize { get; init; }

        // Backward compatibility aliases for HEAD code
        public int TotalItems { get => TotalCount; init => TotalCount = value; }
        public int CurrentPage { get => Page; init => Page = value; }
        public int AllPages { get => TotalPages; init => TotalPages = value; }
    }
}
