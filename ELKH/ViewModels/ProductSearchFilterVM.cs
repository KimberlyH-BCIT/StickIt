using Microsoft.AspNetCore.Mvc.Rendering;

namespace ELKH.ViewModels
{
    /// <summary>
    /// ViewModel for the product search and filter partial component
    /// </summary>
    public class ProductSearchFilterVM
    {
        public string? Search { get; set; }
        public int? CategoryId { get; set; }
        public string CurrentSort { get; set; } = "name_asc";
        public SelectList Categories { get; set; } = new SelectList(new List<object>());
    }
}
