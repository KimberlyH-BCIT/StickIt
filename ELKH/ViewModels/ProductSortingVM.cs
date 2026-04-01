namespace ELKH.ViewModels
{
    /// <summary>
    /// ViewModel for the product sorting controls partial component
    /// </summary>
    public class ProductSortingVM
    {
        public string? Search { get; set; }
        public int? CategoryId { get; set; }
        public string CurrentSort { get; set; } = "name_asc";
        public bool IsPromotionView { get; set; } = false;
    }
}