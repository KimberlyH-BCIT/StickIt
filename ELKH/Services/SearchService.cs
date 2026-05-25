namespace ELKH.Services;

// ╔════════════════════════════════════════════════════════════════════════════════════╗
// ║ SearchService - TABLE OF CONTENTS                                                ║
// ╚════════════════════════════════════════════════════════════════════════════════════╝
//
// OVERVIEW: Multi-tier product search with cache, FTS, and fuzzy fallback paths.
// TABLE OF CONTENTS:
// - SearchNames
// - Normalization helpers
// - FTS and fuzzy fallback helpers

/// <summary>
/// Service for fuzzy product search with multi-tier fallback strategy.
/// Provides fast autocomplete and search results using precomputed suggestions, FTS, and fuzzy matching.
/// </summary>
public class SearchService : ISearchService
{
    #region Fields & Constructor

    private readonly ApplicationDbContext _db;
    private readonly ELKH.Services.FuzzyHelperService _fuzzyHelper;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
    private readonly Microsoft.Extensions.Options.IOptions<ELKH.Configuration.SearchOptions> _searchOptions;
    private static readonly char[] SearchTokenSeparators = [' '];

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
    /// - Fast lookup against the FuzzySuggestions denormalized table
    /// - Populated by FuzzyReindexService background job
    /// - Uses a simple NameNormalized Contains() match as an early suggestion pass
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
        var normQ = SearchTextNormalizer.NormalizeQuery(token);

        // ─────────────────────────────────────────────────────────────
        // TIER 1: Cache lookup
        // Return immediately for repeated identical queries within the 5-minute window.
        // ─────────────────────────────────────────────────────────────
        var cacheKey = $"search_{normQ}";
        var topResults = _searchOptions?.Value?.Fuzzy?.TopResults ?? 10;
        var candidateLimit = _searchOptions?.Value?.Fuzzy?.CandidateLimit ?? 200;
        if (_cache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is List<SearchResultDto> cached)
            return cached;

        void CacheResults(List<SearchResultDto> results)
        {
            using var entry = _cache.CreateEntry(cacheKey);
            entry.Value = results;
            entry.SlidingExpiration = TimeSpan.FromMinutes(5);
        }

        // ─────────────────────────────────────────────────────────────
        // TIER 2: Precomputed suggestions
        // FuzzySuggestions is a pre-built denormalized table populated by FuzzyReindexService.
        // This pass uses a simple NameNormalized Contains() match before falling through
        // to tag, FTS, and fuzzy-search strategies.
        // ─────────────────────────────────────────────────────────────
        var pre = await _db.FuzzySuggestions
            .AsNoTracking()
            .Where(f => f.NameNormalized.Contains(normQ))
            .OrderBy(f => f.Name)
            .Take(topResults)
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
            CacheResults(pre);
            return pre;
        }

        // ─────────────────────────────────────────────────────────────
        // TIER 2.5: Tag search (if no suggestions found)
        // Search for products by tags if the query didn't match product names.
        // Provides additional discovery path for category/keyword searches.
        // ─────────────────────────────────────────────────────────────
        var tagMatches = await _db.Products
            .AsNoTracking()
            .Where(p => p.Tags != null && p.Tags.Contains(normQ) && p.IsActive)
            .OrderBy(p => p.Name)
            .Take(topResults)
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
            CacheResults(tagMatches);
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
            // Join FTS virtual table with Products to get full product details.
            // If the FTS table is unavailable or not yet initialized in a test/local database,
            // swallow that failure and continue to the fuzzy fallback instead of failing the request.
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
            try
            {
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var id = rdr.GetInt32(0);
                    var name = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                    var price = rdr.IsDBNull(2) ? 0m : rdr.GetDecimal(2);
                    var thumb = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3);
                    results.Add(new SearchResultDto { Id = id, Name = name, Price = price, Thumbnail = thumb });
                }
            }
            catch (Exception) when (_db.Database.IsSqlite())
            {
                results.Clear();
            }

            if (results.Count > 0)
            {
                CacheResults(results);
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
        var searchTokens = token.Split(SearchTokenSeparators, StringSplitOptions.RemoveEmptyEntries);

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
            .Take(candidateLimit)
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
            var positions = _fuzzyHelper.FindBestMatchPositions(searchTokens, x.Name ?? string.Empty);

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
        CacheResults(resultList);
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
    private static string NormalizeName(string name)
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
