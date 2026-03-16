namespace ELKH.ViewModels
{
    /// <summary>
    /// Generic wrapper for paginated query results.
    /// Returned by service methods that support paging to avoid leaking IQueryable.
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; init; } = new();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int AllPages => (int)Math.Ceiling((double)TotalItems / PageSize);


        public int TotalPages { get; init; }
        public int TotalCount { get; init; }
    }
}
