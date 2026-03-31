namespace ELKH.ViewModels
{
    /// <summary>
    /// ViewModel for the pagination controls partial component
    /// </summary>
    public class PaginationVM
    {
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public string? Search { get; set; }
        public int? CategoryId { get; set; }
    }
}