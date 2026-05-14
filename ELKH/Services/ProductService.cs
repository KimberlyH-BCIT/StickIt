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
    public class ProductService(ApplicationDbContext db, ISearchService searchService, IProductMapper mapper, ILogger<ProductService> logger) : IProductService
    {
        /// <inheritdoc/>
        public async Task<IEnumerable<ProductVM>> GetAllAsync(CancellationToken ct = default)
        {
            // Include Category so the mapper can populate CategoryName without a second query.
            var products = await db.Products
                .Where(p => !p.IsDeleted)
                .Include(p => p.Category)
                .ToListAsync(ct);
            return mapper.ToViewModels(products);
        }

        /// <inheritdoc/>
        public async Task<PagedResult<ProductVM>> GetPagedCatalogAsync(
            string? search,
            int? categoryId,
            string sort,
            int skip,
            int take,
            CancellationToken ct = default)
        {
            skip = Math.Max(0, skip);
            take = Math.Max(1, take);

            IQueryable<ProductModel> query = db.Products
                .AsNoTracking()
                .Where(p => p.IsActive && !p.IsDeleted);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p => p.Name.Contains(term) || p.Description.Contains(term));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.FkCategoryId == categoryId.Value);
            }

            query = sort switch
            {
                "name_desc" => query.OrderByDescending(p => p.Name).ThenByDescending(p => p.PkProductId),
                "price_low" => query.OrderBy(p => p.Price).ThenBy(p => p.PkProductId),
                "price_high" => query.OrderByDescending(p => p.Price).ThenByDescending(p => p.PkProductId),
                "newest" => query.OrderByDescending(p => p.DateAdded).ThenByDescending(p => p.PkProductId),
                "oldest" => query.OrderBy(p => p.DateAdded).ThenBy(p => p.PkProductId),
                _ => query.OrderBy(p => p.Name).ThenBy(p => p.PkProductId)
            };

            var totalCount = await query.CountAsync(ct);

            var products = await query
                .Include(p => p.Category)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);

            var page = (skip / take) + 1;
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)take);

            return new PagedResult<ProductVM>
            {
                Items = mapper.ToViewModels(products),
                TotalCount = totalCount,
                TotalPages = totalPages,
                Page = page,
                PageSize = take
            };
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
                .Where(p => idList.Contains(p.PkProductId) && !p.IsDeleted)
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
                entity.IsDeleted = true;
                entity.IsActive = false;
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
                // Audit write failure must never block the reindex - log and continue.
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
                .Where(p => !p.IsDeleted && p.IsActive && (
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
                // NonSpacingMark characters are the separated diacritics - drop them.
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            // NFC re-composes any remaining sequences; ToLowerInvariant gives culture-independent casing.
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
        }
    }
}
