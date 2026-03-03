using Microsoft.EntityFrameworkCore;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a paginated list of items.
    /// </summary>
    public class PaginatedListModel<T> : List<T>
    {
        public int PageIndex { get; private set; }
        public int TotalPages { get; private set; }

        /// <summary>
        /// Constructor to initialize the paginated list.
        /// </summary>
        public PaginatedListModel(List<T> items, int count,
                             int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);
            this.AddRange(items);
        }

        /// <summary>
        /// Indicates if there is a previous page.
        /// </summary>
        public bool HasPreviousPage()
        {
            return PageIndex > 1;
        }

        /// <summary>
        /// Indicates if there is a next page.
        /// </summary>
        public bool HasNextPage()
        {
            return PageIndex < TotalPages;
        }

        /// <summary>
        /// Creates a paginated list from an IEnumerable data source.
        /// </summary>
        public static PaginatedListModel<T> Create(IEnumerable<T> source,
                                              int pageIndex,
                                              int pageSize)
        {
            var count = source.Count();
            var items = source.Skip((pageIndex - 1) * pageSize)
                              .Take(pageSize)
                              .ToList();

            return new PaginatedListModel<T>(items, count, pageIndex, pageSize);
        }
    }
}