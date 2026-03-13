using AutoMapper;
using ELKH.Models;
using ELKH.ViewModels;

namespace ELKH.Mapping
{
    /// <summary>
    /// AutoMapper profile for mapping between domain models and view models.
    /// Configures bidirectional mappings for ProductModel &lt;=&gt; ProductVM.
    /// </summary>
    /// <remarks>
    /// - Maps ProductModel to ProductVM for use in views and API responses.
    /// - Maps ProductVM to ProductModel for persistence and updates.
    /// - Handles custom property mapping, including category name, thumbnail, and average rating.
    /// - Ignores NameNormalized on reverse mapping (set by service logic).
    /// </remarks>
    public class AutoMapperProfile : Profile
    {
        /// <summary>
        /// Configures all mappings for the application.
        /// </summary>
        public AutoMapperProfile()
        {
            // ProductModel -> ProductVM
            CreateMap<ProductModel, ProductVM>()
                .ForMember(d => d.ProductId, o => o.MapFrom(s => s.PkProductId))
                .ForMember(d => d.ProductName, o => o.MapFrom(s => s.Name))
                .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category != null ? s.Category.CategoryName : "Unknown"))
                .ForMember(d => d.Thumbnail, o => o.MapFrom(s => s.ProductImage != null ? s.ProductImage.Select(pi => pi.ProductImageURL).FirstOrDefault() ?? string.Empty : string.Empty))
                .ForMember(d => d.AverageRating, o => o.MapFrom(s => s.ProductRatings != null && s.ProductRatings.Any() ? s.ProductRatings.Average(r => r.Rating) : 0));

            // ProductVM -> ProductModel
            CreateMap<ProductVM, ProductModel>()
                .ForMember(d => d.PkProductId, o => o.MapFrom(s => s.ProductId))
                .ForMember(d => d.Name, o => o.MapFrom(s => s.ProductName))
                .ForMember(d => d.NameNormalized, o => o.Ignore());
        }
    }
}
