using System;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ELKH.Data;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Services
{
    /// <summary>
    /// Service for fuzzy product search with multi-tier fallback strategy.
    /// Provides fast autocomplete and search results using precomputed suggestions, FTS, and fuzzy matching.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS
    /// ================================================================================
    /// 1. Fields & Constructor
    /// 2. Public Search Methods
    ///    - SearchNames()                         // Multi-tier search strategy
    /// 3. Search Strategy (in priority order)
    ///    Step 1: Check cache for normalized query
    ///    Step 2: Query precomputed fuzzy suggestions (fast indexed lookup)
    ///    Step 3: FTS query against ProductFTS virtual table
    ///    Step 4: Fuzzy fallback - score candidates with TokenSetRatio
    /// 4. Private Helpers
    ///    - NormalizeName()                       // String normalization (lowercase, diacritics removed)
    /// ================================================================================
    /// 
    /// Search Optimization:
    /// - Results cached for 5 minutes (sliding expiration)
    /// - Precomputed suggestions checked first (fastest)
    /// - FTS used for exact/prefix matching (very fast)
    /// - Fuzzy scoring as last resort (more expensive but comprehensive)
    /// </remarks>
    public class SearchService : ISearchService
    {
        private readonly ApplicationDbContext _db;
        private readonly ELKH.Services.FuzzyHelperService _fuzzyHelper;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
        private readonly Microsoft.Extensions.Options.IOptions<ELKH.Configuration.SearchOptions> _searchOptions;

        /// <summary>
        /// Initializes a new instance of <see cref="SearchService"/>.
        /// </summary>
        /// <param name="db">EF Core context for product and suggestion queries.</param>
        /// <param name="fuzzyHelper">Helper that computes fuzzy match positions for UI highlighting.</param>
        /// <param name="cache">In-memory cache used to store search results for 5 minutes.</param>
        /// <param name="searchOptions">Configuration for fuzzy thresholds, candidate limits, and result caps.</param>
        public SearchService(ApplicationDbContext db, ELKH.Services.FuzzyHelperService fuzzyHelper, Microsoft.Extensions.Caching.Memory.IMemoryCache cache, Microsoft.Extensions.Options.IOptions<ELKH.Configuration.SearchOptions> searchOptions)
        {
            _db = db;
            _fuzzyHelper = fuzzyHelper;
            _cache = cache;
            _searchOptions = searchOptions;
        }

        /// <summary>
        /// Search product names using the following strategy (in order):
        /// 1. Return cached results for the normalized query.
        /// 2. Query precomputed fuzzy suggestions (fast lookup).
        /// 3. Run an FTS query against the ProductFTS virtual table.
        /// 4. As a last resort, run a candidate scan and fuzzy-score the results.
        ///
        /// The method caches successful result sets for a brief period to avoid
        /// repeated work for the same query.
        /// </summary>
        public async Task<List<SearchResultDto>> SearchNames(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return new List<SearchResultDto>();

            // Strip quote characters that would break the FTS5 MATCH syntax before normalizing.
            var token = q.Trim().Replace('"', ' ').Replace('\'', ' ');
            var normQ = NormalizeName(token);

            // --- Tier 1: Cache ---
            // Return immediately for repeated identical queries within the 5-minute window.
            var cacheKey = $"search_{normQ}";
            if (_cache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is List<SearchResultDto> cached) return cached;

            // --- Tier 2: Precomputed suggestions (fast indexed lookup) ---
            // FuzzySuggestions is a pre-built denormalized table populated by FuzzyReindexService.
            // A simple Contains() on NameNormalized is fast because the column is indexed.
            var pre = await _db.FuzzySuggestions
                .Where(f => f.NameNormalized.Contains(normQ))
                .OrderBy(f => f.Name)
                .Take(_searchOptions?.Value?.Fuzzy?.TopResults ?? 10)
                .Select(f => new SearchResultDto { Id = f.PkProductId, Name = f.Name, Price = f.Price, Thumbnail = f.Thumbnail })
                .ToListAsync();

            if (pre.Any())
            {
                // CreateEntry + manual property assignment is used instead of the IMemoryCache.Set
                // extension method to avoid a dependency on the Microsoft.Extensions.Caching.Memory
                // extension package that may not be available in all test environments.
                using (var entry = _cache.CreateEntry(cacheKey))
                {
                    entry.Value = pre;
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                }
                return pre;
            }

            // --- Tier 2.5: Tag search (if no suggestions found) ---
            // Search for products by tags if the query didn't match product names
            var tagMatches = await _db.Product
                .Where(p => p.Tags.Contains(normQ) && p.IsActive)
                .OrderBy(p => p.Name)
                .Take(_searchOptions?.Value?.Fuzzy?.TopResults ?? 10)
                .Select(p => new SearchResultDto
                {
                    Id = p.PkProductId,
                    Name = p.Name,
                    Price = p.Price,
                    Thumbnail = p.ProductImage!.Select(pi => pi.ProductImageURL).FirstOrDefault() ?? string.Empty
                })
                .ToListAsync();

            if (tagMatches.Any())
            {
                using (var entry = _cache.CreateEntry(cacheKey))
                {
                    entry.Value = tagMatches;
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                }
                return tagMatches;
            }

            // --- Tier 3: FTS query via raw ADO.NET ---
            // EF Core does not support SQLite FTS5 MATCH natively, so we drop down to a raw
            // DbCommand. The token + "*" suffix enables prefix matching (e.g. "lap*" matches "laptop").
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT p.PkProductId as Id, p.Name as Name, p.Price as Price,
IFNULL((SELECT ProductImageURL FROM ProductImages pi WHERE pi.FkProductId = p.PkProductId LIMIT 1), '') as Thumbnail
FROM ProductFTS f
JOIN Products p ON p.PkProductId = f.rowid
WHERE f.Name MATCH @p
ORDER BY p.Name
LIMIT 10;";
                var p = cmd.CreateParameter(); p.ParameterName = "@p"; p.Value = token + "*"; cmd.Parameters.Add(p);
                var results = new List<SearchResultDto>();
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var id = rdr.GetInt32(0);
                    var name = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                    var price = rdr.IsDBNull(2) ? 0m : rdr.GetDecimal(2);
                    var thumb = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3);
                    results.Add(new SearchResultDto { Id = id, Name = name, Price = price, Thumbnail = thumb });
                }

                if (results.Any())
                {
                    using (var entry = _cache.CreateEntry(cacheKey))
                    {
                        entry.Value = results;
                        entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                    }
                    return results;
                }
            }
            finally { await conn.CloseAsync(); }

            // --- Tier 4: Fuzzy fallback ---
            // Use a 3-character prefix to pre-filter candidates before expensive fuzzy scoring,
            // capping the scan at CandidateLimit (default 200) to bound worst-case latency.
            var normQuery = normQ;
            var prefix = normQuery.Length >= 3 ? normQuery.Substring(0, 3) : normQuery;
            var candidates = await _db.Product
                .Select(p => new { p.PkProductId, p.Name, p.Price, p.NameNormalized, p.Tags, Thumbnail = p.ProductImage!.Select(pi => pi.ProductImageURL).FirstOrDefault() })
                .Where(p => p.NameNormalized.Contains(prefix) || p.NameNormalized.StartsWith(normQuery) || p.Tags.Contains(normQuery))
                .Take(_searchOptions?.Value?.Fuzzy?.CandidateLimit ?? 200)
                .ToListAsync();

            // Score candidates with TokenSetRatio (handles word-order variation) and take the top results.
            // Also score against tags for better discoverability
            var scoredPre = candidates.Select(c => new
            {
                c.PkProductId,
                c.Name,
                c.Price,
                c.Thumbnail,
                Score = Math.Max(
                    FuzzySharp.Fuzz.TokenSetRatio(normQuery, c.NameNormalized ?? string.Empty),
                    FuzzySharp.Fuzz.TokenSetRatio(normQuery, c.Tags ?? string.Empty)
                )
            })
                .OrderByDescending(x => x.Score)
                .Take(_searchOptions?.Value?.Fuzzy?.TopResults ?? 10)
                .ToList();

            var resultList = new List<SearchResultDto>();
            foreach (var x in scoredPre)
            {
                // Compute character-level match positions so the client can render
                // bold highlight spans without any additional string parsing.
                var tokens = token.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var positions = _fuzzyHelper.FindBestMatchPositions(tokens, x.Name ?? string.Empty);
                var dto = new SearchResultDto { Id = x.PkProductId, Name = x.Name ?? string.Empty, Price = x.Price, Thumbnail = x.Thumbnail ?? string.Empty };
                dto.Matches.AddRange(positions);
                resultList.Add(dto);
            }

            using (var entry = _cache.CreateEntry(cacheKey))
            {
                entry.Value = resultList;
                entry.SlidingExpiration = TimeSpan.FromMinutes(5);
            }
            return resultList;
        }

        /// <summary>
        /// Produces a lowercase, diacritic-free version of a string for consistent
        /// cache key generation and candidate comparison during fuzzy search.
        /// </summary>
        private string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            // NFD splits composite characters into base letter + combining marks.
            var s = name.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                // Drop NonSpacingMark characters (the separated diacritics/accents).
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            // NFC re-composes; ToLowerInvariant gives culture-independent case folding.
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
        }
    }
}
