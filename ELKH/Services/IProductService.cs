using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ELKH.Models;
using ELKH.ViewModels;

namespace ELKH.Services
{
    /// <summary>
    /// Contract for product catalog operations including CRUD, search, and FTS index management.
    /// Implementations are responsible for AutoMapper projection and normalized-name indexing.
    /// </summary>
    public interface IProductService
    {
        /// <summary>
        /// Returns all products with their <c>Category</c> navigation property populated.
        /// Intended to be served via the output cache on the product listing page.
        /// </summary>
        Task<IEnumerable<ProductVM>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Returns a single product by primary key with its category loaded,
        /// or <see langword="null"/> if no product with <paramref name="id"/> exists.
        /// Uses a compiled EF Core query for reduced translation overhead on this hot path.
        /// </summary>
        Task<ProductVM?> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Returns a dictionary of products keyed by primary key for efficient batch lookups,
        /// avoiding N+1 queries when enriching cart or order line items.
        /// Returns an empty dictionary if <paramref name="ids"/> is empty.
        /// </summary>
        Task<Dictionary<int, ProductVM>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default);

        /// <summary>
        /// Persists a new product from the view model and immediately sets its
        /// normalized name for FTS search indexing.
        /// </summary>
        Task CreateAsync(ProductVM vm, CancellationToken ct = default);

        /// <summary>
        /// Locates the existing product by <see cref="ProductVM.ProductId"/>, maps the view model
        /// changes onto it, and refreshes the normalized name for search index consistency.
        /// No-ops silently if the product no longer exists.
        /// </summary>
        Task UpdateAsync(ProductVM vm, CancellationToken ct = default);

        /// <summary>
        /// Permanently removes a product from the catalog.
        /// No-ops silently if the product no longer exists.
        /// </summary>
        Task DeleteAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Inserts any products not yet present in the <c>ProductFTS</c> virtual table
        /// and writes an audit log entry describing why the reindex was triggered.
        /// </summary>
        /// <param name="reason">Human-readable description of why the reindex was requested (stored in the audit log).</param>
        Task ReindexFTSAsync(string reason, CancellationToken ct = default);

        /// <summary>
        /// Delegates to <see cref="ISearchService.SearchNames"/> for compatibility.
        /// Prefer injecting <see cref="ISearchService"/> directly in search-specific callers.
        /// </summary>
        Task<IEnumerable<SearchResultDto>> SearchNames(string q, CancellationToken ct = default);

        /// <summary>Returns all categories ordered alphabetically by name, used to populate form dropdowns.</summary>
        Task<IEnumerable<CategoryModel>> GetCategoriesAsync(CancellationToken ct = default);
    }
}

