using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ELKH.Data;
using ELKH.ViewModels;
using ELKH.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELKH.Services
{
    /// <summary>
    /// Implementation of <see cref="IProductService"/> backed by EF Core with manual mapping.
    /// Delegates search operations to <see cref="ISearchService"/> and uses
    /// <see cref="CompiledQueries"/> for hot-path single-product lookups.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ==================
    /// 1. Constructor & Dependencies (lines 33-40)
    /// 2. Product Retrieval
    ///    - GetAllAsync() - Fetch all products with category (lines 43-48)
    ///    - GetByIdAsync() - Single product by ID (lines 51-57)
    ///    - GetByIdsAsync() - Batch fetch for cart/order enrichment (lines 60-74)
    /// 3. Product Search
    ///    - SearchNames() - Fuzzy name search via ISearchService (lines 77-80)
    /// 4. Product Mutation
    ///    - CreateAsync() - Add new product with normalized name (lines 83-91)
    ///    - UpdateAsync() - Update existing product fields (lines 94-112)
    ///    - DeleteAsync() - Remove product (lines 115-122)
    /// 5. Full-Text Search Management
    ///    - ReindexFTSAsync() - Rebuild fuzzy search index (lines 125-134)
    /// 6. Helpers
    ///    - NormalizeName() - Lowercase + remove diacritics (lines 137-142)
    /// </remarks>
    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _db;
        private readonly ISearchService _searchService;
        private readonly IProductMapper _mapper;
        private readonly ILogger<ProductService> _logger;

        public ProductService(ApplicationDbContext db, ISearchService searchService, IProductMapper mapper, ILogger<ProductService> logger)
        {
            _db = db;
            _searchService = searchService;
            _mapper        = mapper;
            _logger        = logger;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<ProductVM>> GetAllAsync(CancellationToken ct = default)
        {
            // Include Category so the mapper can populate CategoryName without a second query.
            var products = await _db.Products.Include(p => p.Category).ToListAsync(ct);
            return _mapper.ToViewModels(products);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<SearchResultDto>> SearchNames(string q, CancellationToken ct = default)
        {
            return await _searchService.SearchNames(q);
        }

        /// <inheritdoc/>
        public async Task<ProductVM?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            // CompiledQueries.GetProductById avoids repeated EF query translation on this hot path.
            var p = await CompiledQueries.GetProductById(_db, id, ct);
            if (p == null) return null;
            return _mapper.ToViewModel(p);
        }

        /// <inheritdoc/>
        public async Task<Dictionary<int, ProductVM>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            var idList = ids.ToList();
            if (idList.Count == 0)
                return [];

            // Fetch all matching products in one query; build a dictionary keyed by ProductId
            // so callers can do O(1) lookups when enriching cart or order line items.
            var products = await _db.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => idList.Contains(p.PkProductId))
                .ToListAsync(ct);

            var viewModels = _mapper.ToViewModels(products);
            return viewModels.ToDictionary(vm => vm.ProductId);
        }

        /// <inheritdoc/>
        public async Task CreateAsync(ProductVM vm, CancellationToken ct = default)
        {
            var entity = _mapper.ToModel(vm);
            // Normalize the name immediately so the entity is search-ready before it is persisted.
            entity.NameNormalized = NormalizeName(entity.Name);
            _db.Products.Add(entity);
            await _db.SaveChangesAsync(ct);
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(ProductVM vm, CancellationToken ct = default)
        {
            var entity = await _db.Products.FindAsync(new object[] { vm.ProductId }, ct);
            if (entity == null) return;

            // Manual mapping of changed fields onto the tracked entity
            entity.Name = vm.ProductName;
            entity.Description = vm.Description;
            entity.Price = vm.Price;
            entity.DiscountPercent = vm.DiscountPercent;
            entity.StockQuantity = vm.StockQuantity;
            entity.FkCategoryId = vm.CategoryId;
            entity.IsActive = vm.IsActive;

            // Re-normalize after mapping in case the product name changed.
            entity.NameNormalized = NormalizeName(entity.Name);
            await _db.SaveChangesAsync(ct);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var entity = await _db.Products.FindAsync(new object[] { id }, ct);
            if (entity != null)
            {
                _db.Products.Remove(entity);
                await _db.SaveChangesAsync(ct);
            }
        }

        /// <inheritdoc/>
        public async Task ReindexFTSAsync(string reason, CancellationToken ct = default)
        {
            // Insert only the products not already present in the FTS virtual table,
            // using the product PK as both the FTS rowid and the stored PkProductId column.
            var sql = @"INSERT INTO ProductFTS(rowid, Name, PkProductId)
SELECT PkProductId, Name, PkProductId FROM Products
WHERE PkProductId NOT IN (SELECT rowid FROM ProductFTS);
";
            await _db.Database.ExecuteSqlRawAsync(sql, ct);

            try
            {
                // Write an audit entry so admins can track when and why reindexes occurred.
                // Wrapped in try/catch so an audit write failure never blocks the reindex itself.
                _db.Add(new ELKH.Models.AuditEntryModel { Action = "ReindexFTS", Actor = "system", Timestamp = System.DateTime.UtcNow, AffectedKeysCount = 0, Details = "Reindexed ProductFTS table", Reason = reason });
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // Audit write failure must never block the reindex — log and continue.
                _logger.LogWarning(ex, "Failed to persist ReindexFTS audit entry");
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<CategoryModel>> GetCategoriesAsync(CancellationToken ct = default)
        {
            return await _db.Categories.OrderBy(c => c.CategoryName).ToListAsync(ct);
        }

        /// <summary>
        /// Produces a normalized, lowercase, diacritic-free version of a product name
        /// for case-insensitive and accent-insensitive FTS indexing.
        /// </summary>
        /// <example><c>NormalizeName("Crème Brûlée")</c> returns <c>"creme brulee"</c>.</example>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            // NFD decomposes composite characters into base letter + separate combining marks.
            // Example: "é" (U+00E9) → "e" (U+0065) + combining acute accent (U+0301).
            var s = name.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                // NonSpacingMark characters are the separated diacritics — drop them.
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            // NFC re-composes any remaining sequences; ToLowerInvariant gives culture-independent casing.
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
        }
    }
}
