using ELKH.Models;
using ELKH.ViewModels;

namespace ELKH.Services
{
    /// <summary>
    /// Lightweight manual mapper for ProductModel <-> ProductVM conversions.
    /// Replaces AutoMapper to avoid version compatibility issues.
    /// </summary>
    public class ProductMapper : IProductMapper
    {
        public ProductVM ToViewModel(ProductModel model)
        {
            return new ProductVM
            {
                ProductId = model.PkProductId,
                ProductName = model.Name,
                Description = model.Description,
                Price = model.Price,
                DiscountPercent = model.DiscountPercent,
                StockQuantity = model.StockQuantity ?? 0,
                CategoryId = model.FkCategoryId,
                CategoryName = model.Category?.CategoryName ?? "Unknown",
                Thumbnail = model.ProductImage?.FirstOrDefault()?.ProductImageURL ?? string.Empty,
                IsActive = model.IsActive,
                AverageRating = model.ProductRatings?.Any() == true
                    ? model.ProductRatings.Average(r => r.Rating)
                    : 0.0
            };
        }

        public List<ProductVM> ToViewModels(List<ProductModel> models)
        {
            return models.Select(ToViewModel).ToList();
        }

        public ProductModel ToModel(ProductVM viewModel)
        {
            return new ProductModel
            {
                PkProductId = viewModel.ProductId,
                Name = viewModel.ProductName,
                Description = viewModel.Description,
                Price = viewModel.Price,
                DiscountPercent = viewModel.DiscountPercent,
                StockQuantity = viewModel.StockQuantity,
                FkCategoryId = viewModel.CategoryId,
                IsActive = viewModel.IsActive
                // NameNormalized is set by ProductService logic, not by mapper
            };
        }
    }
}
