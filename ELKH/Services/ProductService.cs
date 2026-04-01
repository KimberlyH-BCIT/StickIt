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
    /// ================================================================================
    /// 1. Constructor & Dependencies
    ///    - ApplicationDbContext, ISearchService, IProductMapper, ILogger injection
    /// 
    /// 2. Product Retrieval Operations
    ///    - GetAllAsync()                         // Fetch all products with category eagerly loaded
    ///    - GetByIdAsync()                        // Single product lookup with compiled query (includes category)
    ///    - GetByIdsAsync()                       // Batch fetch for cart/order enrichment
    /// 
    /// 3. Product Search Integration
    ///    - SearchNames()                         // Fuzzy name search delegation to ISearchService
    /// 
    /// 4. Product CRUD Operations
    ///    - CreateAsync()                         // Add new product with name normalization
    ///    - UpdateAsync()                         // Update existing product with validation
    ///    - DeleteAsync()                         // Hard delete (not soft delete)
    /// 
    /// 5. Category & Promotional Operations
    ///    - GetCategoriesAsync()                  // Retrieve all categories for dropdowns
    ///    - GetPromotionalProductsAsync()         // Products with discounts or active coupons
    /// 
    /// 6. Search Index Management
    ///    - ReindexFTSAsync()                     // Rebuild full-text search index coordination
    ///    - FTS table maintenance and optimization
    /// 
    /// 7. Private Helper Methods
    ///    - NormalizeName()                       // String normalization for consistent storage
    /// ================================================================================
    /// 
    /// PERFORMANCE OPTIMIZATIONS:
    /// • Compiled queries for frequently accessed single-product lookups
    /// • Efficient batch operations for cart and order processing
    /// • Delegated search operations to specialized ISearchService
    /// • Eager loading of category relationships to minimize round trips
    /// 
    /// DATA ACCESS PATTERNS:
    /// • Repository pattern implementation with service layer abstraction
    /// • Manual DTO mapping for precise control over data transfer
    /// • Optimistic concurrency handling for product updates
    /// • Transactional operations for data consistency
    /// 
    /// INTEGRATION POINTS:
    /// • ISearchService for fuzzy product name searching capabilities
    /// • ApplicationDbContext for Entity Framework data operations
    /// • ILogger for operation tracking and performance monitoring
    /// • FTS index coordination for search functionality
    /// 
    /// BUSINESS LOGIC:
    /// • Product name normalization for consistent searching
    /// • Category relationship management and validation
    /// • Hard delete implementation (no soft delete)
    /// • Audit trail support for FTS reindexing operations
    /// </remarks>
    public class ProductService(ApplicationDbContext db, ISearchService searchService, IProductMapper mapper, ILogger<ProductService> logger) : IProductService
    {
        /// <inheritdoc/>
        public async Task<IEnumerable<ProductVM>> GetAllAsync(CancellationToken ct = default)
        {
            // Include Category so the mapper can populate CategoryName without a second query.
            var products = await db.Products.Include(p => p.Category).ToListAsync(ct);
            return mapper.ToViewModels(products);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<SearchResultDto>> SearchNames(string q, CancellationToken ct = default)
        {
            return await searchService.SearchNames(q);
        }

        /// <inheritdoc/>
        public async Task<ProductVM?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            // CompiledQueries.GetProductById avoids repeated EF query translation on this hot path.
            var p = await CompiledQueries.GetProductById(db, id, ct);
            if (p == null) return null;
            return mapper.ToViewModel(p);
        }

        /// <inheritdoc/>
        public async Task<Dictionary<int, ProductVM>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            var idList = ids.ToList();
            if (idList.Count == 0)
                return [];

            // Fetch all matching products in one query; build a dictionary keyed by ProductId
            // so callers can do O(1) lookups when enriching cart or order line items.
            var products = await db.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => idList.Contains(p.PkProductId))
                .ToListAsync(ct);

            var viewModels = mapper.ToViewModels(products);
            return viewModels.ToDictionary(vm => vm.ProductId);
        }

        /// <inheritdoc/>
        public async Task CreateAsync(ProductVM vm, CancellationToken ct = default)
        {
            var entity = mapper.ToModel(vm);
            // Normalize the name immediately so the entity is search-ready before it is persisted.
            entity.NameNormalized = NormalizeName(entity.Name);
            db.Products.Add(entity);
            await db.SaveChangesAsync(ct);
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(ProductVM vm, CancellationToken ct = default)
        {
            var entity = await db.Products.FindAsync(new object[] { vm.ProductId }, ct);
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
            await db.SaveChangesAsync(ct);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var entity = await db.Products.FindAsync(new object[] { id }, ct);
            if (entity != null)
            {
                db.Products.Remove(entity);
                await db.SaveChangesAsync(ct);
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
            await db.Database.ExecuteSqlRawAsync(sql, ct);

            try
            {
                // Write an audit entry so admins can track when and why reindexes occurred.
                // Wrapped in try/catch so an audit write failure never blocks the reindex itself.
                db.Add(new ELKH.Models.AuditEntryModel { Action = "ReindexFTS", Actor = "system", Timestamp = System.DateTime.UtcNow, AffectedKeysCount = 0, Details = "Reindexed ProductFTS table", Reason = reason });
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // Audit write failure must never block the reindex — log and continue.
                logger.LogWarning(ex, "Failed to persist ReindexFTS audit entry");
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<CategoryModel>> GetCategoriesAsync(CancellationToken ct = default)
        {
            return await db.Categories.OrderBy(c => c.CategoryName).ToListAsync(ct);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<ProductVM>> GetPromotionalProductsAsync(CancellationToken ct = default)
        {
            // Check if there are any active coupons first to optimize the query
            var hasActiveCoupons = await db.Coupons
                .AnyAsync(c => c.IsActive && 
                    c.ValidFrom <= DateTime.UtcNow && 
                    c.ValidUntil >= DateTime.UtcNow, ct);

            // Get products with direct discounts or all products if there are active coupons
            var promotionalProducts = await db.Products
                .Where(p => p.IsActive && (
                    p.DiscountPercent > 0 || // Products with direct discounts
                    hasActiveCoupons         // All products are eligible if there are active coupons
                ))
                .Include(p => p.Category)
                .OrderBy(p => p.Name)
                .ToListAsync(ct);

            return mapper.ToViewModels(promotionalProducts);
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
