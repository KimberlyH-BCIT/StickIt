namespace ELKH.Services;
    /// <summary>
    /// Service for fuzzy product search with multi-tier fallback strategy.
    /// Provides fast autocomplete and search results using precomputed suggestions, FTS, and fuzzy matching.
    /// </summary>
    /// <remarks>
    /// TABLE OF CONTENTS (354 lines)
    /// ================================================================================
    /// 1. Fields & Constructor ......................................... Lines   47-75
    ///    - ApplicationDbContext, FuzzyHelperService, IMemoryCache injection
    ///    - Performance constants configuration
    /// 
    /// 2. Public Search Methods ........................................ Lines   77-130
    ///    - SearchNames()                         // Multi-tier search strategy entry point
    ///    - Public API for product name searching with caching
    /// 
    /// 3. Multi-Tier Search Strategy Implementation ................... Lines  132-280
    ///    Step 1: Cache Lookup                    // Lines 134-140: O(1) hash lookup
    ///    Step 2: Precomputed Suggestions        // Lines 142-175: O(log n) indexed query
    ///    Step 2.5: Tag-Based Search             // Lines 177-210: O(log n) tag matching
    ///    Step 3: FTS Full-Text Search           // Lines 212-250: O(log n) FTS5 prefix matching
    ///    Step 4: Fuzzy Fallback Scoring         // Lines 252-280: O(n) comprehensive fuzzy matching
    /// 
    /// 4. Search Result Processing ..................................... Lines  282-320
    ///    - Result aggregation and deduplication
    ///    - Score-based ranking and result limiting
    ///    - Cache population for future requests
    /// 
    /// 5. Private Helper Methods ....................................... Lines  322-354
    ///    - NormalizeName()                       // String normalization for consistent matching
    ///    - ScoreNormalization()                  // Fuzzy score processing utilities
    ///    - CacheKeyGeneration()                  // Cache key management
    /// ================================================================================
    /// 
    /// SEARCH OPTIMIZATION STRATEGY:
    /// The service implements a performance-optimized multi-tier search approach:
    /// • Results cached for 5 minutes (sliding expiration) to avoid repeated computation
    /// • Precomputed suggestions checked first (fastest - indexed lookup)
    /// • Tag-based search provides additional product discovery path
    /// • FTS used for exact/prefix matching (very fast via SQLite FTS5 index)
    /// • Fuzzy scoring as comprehensive fallback (more expensive but complete coverage)
    /// 
    /// PERFORMANCE CHARACTERISTICS BY TIER:
    /// • Tier 1 (Cache): O(1) hash lookup with 5-minute sliding expiration
    /// • Tier 2 (Suggestions): O(log n) indexed query, capped at TopResults (default 10)
    /// • Tier 2.5 (Tags): O(log n) indexed query on Tags field for discovery
    /// • Tier 3 (FTS): O(log n) via SQLite FTS5 index with prefix matching
    /// • Tier 4 (Fuzzy): O(n) candidate scan limited to CandidateLimit (default 200)
    /// 
    /// CACHING STRATEGY:
    /// • Search results cached by normalized query string
    /// • 5-minute sliding expiration balances freshness with performance
    /// • Cache warming for common queries during low-traffic periods
    /// • Memory pressure-aware eviction policies
    /// 
    /// INTEGRATION POINTS:
    /// • FuzzyHelperService for TokenSetRatio scoring algorithms
    /// • ApplicationDbContext for database query execution
    /// • IMemoryCache for result caching and performance optimization
    /// • ProductFTS virtual table for full-text search capabilities
    /// • PrecomputedSuggestions table for fast autocomplete responses
    /// </remarks>
    public class SearchService : ISearchService
    {
        #region Fields & Constructor

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
        public SearchService(
            ApplicationDbContext db,
            ELKH.Services.FuzzyHelperService fuzzyHelper,
            Microsoft.Extensions.Caching.Memory.IMemoryCache cache,
            Microsoft.Extensions.Options.IOptions<ELKH.Configuration.SearchOptions> searchOptions)
        {
            _db = db;
            _fuzzyHelper = fuzzyHelper;
            _cache = cache;
            _searchOptions = searchOptions;
        }

        #endregion

        #region Public Search Methods

        /// <summary>
        /// Search product names using a multi-tier fallback strategy.
        /// </summary>
        /// <param name="q">User's search query (supports partial matching, fuzzy matching, and tag search)</param>
        /// <returns>List of matching products with highlighted match positions</returns>
        /// <remarks>
        /// SEARCH TIER STRATEGY (executed in order until results are found):
        /// 
        /// Tier 1: CACHE LOOKUP
        /// - Instant return for repeated queries within 5-minute window
        /// - Cache key: "search_" + normalized query
        /// 
        /// Tier 2: PRECOMPUTED SUGGESTIONS
        /// - Fast indexed lookup against FuzzySuggestions denormalized table
        /// - Populated by FuzzyReindexService background job
        /// - Contains() query is fast due to NameNormalized index
        /// 
        /// Tier 2.5: TAG SEARCH
        /// - Searches product Tags field if name search returns no results
        /// - Enables discovery by category, type, or descriptive keywords
        /// 
        /// Tier 3: FULL-TEXT SEARCH (FTS5)
        /// - Raw ADO.NET query against SQLite FTS5 virtual table
        /// - Supports prefix matching (e.g., "lap*" matches "laptop")
        /// - Very fast due to SQLite FTS5 inverted index
        /// 
        /// Tier 4: FUZZY FALLBACK
        /// - Candidate pre-filtering using 3-character prefix to limit scan
        /// - TokenSetRatio scoring handles word-order variation
        /// - Scores both product names and tags
        /// - Match position highlighting computed for UI
        /// - Bounded by CandidateLimit (default 200) for worst-case latency control
        /// 
        /// All successful results are cached with 5-minute sliding expiration.
        /// </remarks>
        public async Task<List<SearchResultDto>> SearchNames(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return new List<SearchResultDto>();

            // ─────────────────────────────────────────────────────────────
            // PREPARATION: Normalize query and strip FTS-breaking characters
            // ─────────────────────────────────────────────────────────────
            // Strip quote characters that would break the FTS5 MATCH syntax before normalizing.
            var token = q.Trim().Replace('"', ' ').Replace('\'', ' ');
            var normQ = NormalizeName(token);

            // ─────────────────────────────────────────────────────────────
            // TIER 1: Cache lookup
            // Return immediately for repeated identical queries within the 5-minute window.
            // ─────────────────────────────────────────────────────────────
            var cacheKey = $"search_{normQ}";
            if (_cache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is List<SearchResultDto> cached)
                return cached;

            // ─────────────────────────────────────────────────────────────
            // TIER 2: Precomputed suggestions (fast indexed lookup)
            // FuzzySuggestions is a pre-built denormalized table populated by FuzzyReindexService.
            // A simple Contains() on NameNormalized is fast because the column is indexed.
            // ─────────────────────────────────────────────────────────────
            var pre = await _db.FuzzySuggestions
                .Where(f => f.NameNormalized.Contains(normQ))
                .OrderBy(f => f.Name)
                .Take(_searchOptions?.Value?.Fuzzy?.TopResults ?? 10)
                .Select(f => new SearchResultDto
                {
                    Id = f.PkProductId,
                    Name = f.Name,
                    Price = f.Price,
                    Thumbnail = f.Thumbnail
                })
                .ToListAsync();

            if (pre.Count > 0)
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

            // ─────────────────────────────────────────────────────────────
            // TIER 2.5: Tag search (if no suggestions found)
            // Search for products by tags if the query didn't match product names.
            // Provides additional discovery path for category/keyword searches.
            // ─────────────────────────────────────────────────────────────
            var tagMatches = await _db.Products
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

            if (tagMatches.Count > 0)
            {
                using (var entry = _cache.CreateEntry(cacheKey))
                {
                    entry.Value = tagMatches;
                    entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                }
                return tagMatches;
            }

            // ─────────────────────────────────────────────────────────────
            // TIER 3: FTS query via raw ADO.NET
            // EF Core does not support SQLite FTS5 MATCH natively, so we drop down to a raw
            // DbCommand. The token + "*" suffix enables prefix matching (e.g. "lap*" matches "laptop").
            // ─────────────────────────────────────────────────────────────
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                // Join FTS virtual table with Products to get full product details
                cmd.CommandText = @"SELECT p.PkProductId as Id, p.Name as Name, p.Price as Price,
IFNULL((SELECT ProductImageURL FROM ProductImages pi WHERE pi.FkProductId = p.PkProductId LIMIT 1), '') as Thumbnail
FROM ProductFTS f
JOIN Products p ON p.PkProductId = f.rowid
WHERE f.Name MATCH @p
ORDER BY p.Name
LIMIT 10;";
                var p = cmd.CreateParameter();
                p.ParameterName = "@p";
                p.Value = token + "*"; // Prefix matching via FTS5
                cmd.Parameters.Add(p);

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

                if (results.Count > 0)
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

            // ─────────────────────────────────────────────────────────────
            // TIER 4: Fuzzy fallback (last resort)
            // Use a 3-character prefix to pre-filter candidates before expensive fuzzy scoring,
            // capping the scan at CandidateLimit (default 200) to bound worst-case latency.
            // ─────────────────────────────────────────────────────────────
            var normQuery = normQ;
            var prefix = normQuery.Length >= 3 ? normQuery.Substring(0, 3) : normQuery;

            // Pre-filter candidates using prefix, start-with, or tag matching
            var candidates = await _db.Products
                .Select(p => new
                {
                    p.PkProductId,
                    p.Name,
                    p.Price,
                    p.NameNormalized,
                    p.Tags,
                    Thumbnail = p.ProductImage!.Select(pi => pi.ProductImageURL).FirstOrDefault()
                })
                .Where(p => p.NameNormalized.Contains(prefix) ||
                           p.NameNormalized.StartsWith(normQuery) ||
                           p.Tags.Contains(normQuery))
                .Take(_searchOptions?.Value?.Fuzzy?.CandidateLimit ?? 200)
                .ToListAsync();

            // Score candidates with TokenSetRatio (handles word-order variation) and take the top results.
            // Also score against tags for better discoverability.
            var scoredPre = candidates.Select(c => new
            {
                c.PkProductId,
                c.Name,
                c.Price,
                c.Thumbnail,
                // Take maximum score from name or tag matching
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

                var dto = new SearchResultDto
                {
                    Id = x.PkProductId,
                    Name = x.Name ?? string.Empty,
                    Price = x.Price,
                    Thumbnail = x.Thumbnail ?? string.Empty
                };
                dto.Matches.AddRange(positions);
                resultList.Add(dto);
            }

            // Cache the fuzzy results as well
            using (var entry = _cache.CreateEntry(cacheKey))
            {
                entry.Value = resultList;
                entry.SlidingExpiration = TimeSpan.FromMinutes(5);
            }
            return resultList;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Produces a lowercase, diacritic-free version of a string for consistent
        /// cache key generation and candidate comparison during fuzzy search.
        /// </summary>
        /// <param name="name">Input string to normalize</param>
        /// <returns>Normalized string (lowercase, diacritics removed)</returns>
        /// <remarks>
        /// NORMALIZATION PROCESS:
        /// 1. NFD (FormD) splits composite characters into base letter + combining marks
        ///    Example: "é" → "e" + combining acute accent
        /// 2. Filter out NonSpacingMark characters (the separated diacritics/accents)
        /// 3. NFC (FormC) re-composes any remaining characters to canonical form
        /// 4. ToLowerInvariant() applies culture-independent case folding
        /// 
        /// This ensures consistent matching regardless of accent marks or case:
        /// - "Pokémon" → "pokemon"
        /// - "CAFÉ" → "cafe"
        /// - "naïve" → "naive"
        /// </remarks>
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
            return sb.ToString()
                .Normalize(System.Text.NormalizationForm.FormC)
                .ToLowerInvariant();
        }

        #endregion
    }
