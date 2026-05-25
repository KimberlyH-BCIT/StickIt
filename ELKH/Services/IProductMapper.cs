using ELKH.Models;
using ELKH.ViewModels;

namespace ELKH.Services
{
    /// <summary>
    /// Simple mapper interface for Product domain model and view model conversions.
    /// Replaces AutoMapper with lightweight manual mapping.
    /// </summary>
    public interface IProductMapper
    {
        /// <summary>
        /// Maps a single ProductModel to ProductVM.
        /// </summary>
        ProductVM ToViewModel(ProductModel model);

        /// <summary>
        /// Maps a list of ProductModels to ProductVMs.
        /// </summary>
        List<ProductVM> ToViewModels(List<ProductModel> models);

        /// <summary>
        /// Maps a ProductVM to ProductModel (for updates/creates).
        /// </summary>
        ProductModel ToModel(ProductVM viewModel);
    }
}
