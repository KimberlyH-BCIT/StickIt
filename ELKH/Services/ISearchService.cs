using System.Collections.Generic;
using System.Threading.Tasks;

namespace ELKH.Services
{
    /// <summary>
    /// Data transfer object for search results with match highlighting.
    /// </summary>
    public class SearchResultDto
    {
        /// <summary>Product ID</summary>
        public int Id { get; set; }

        /// <summary>Product name</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Product price</summary>
        public decimal Price { get; set; }

        /// <summary>Product thumbnail image URL</summary>
        public string Thumbnail { get; set; } = string.Empty;

        /// <summary>
        /// Match positions for UI highlighting (start index, length).
        /// Each tuple represents a substring that matches the search query.
        /// </summary>
        public List<(int start, int length)> Matches { get; set; } = new List<(int start, int length)>();
    }

    /// <summary>
    /// Contract for product search operations with fuzzy matching.
    /// Provides multi-tier search strategy for optimal performance and coverage.
    /// </summary>
    public interface ISearchService
    {
        /// <summary>
        /// Searches product names using multi-tier fallback strategy.
        /// </summary>
        /// <param name="q">Search query string</param>
        /// <returns>List of search results with match positions for highlighting</returns>
        /// <remarks>
        /// Multi-tier search strategy (in priority order):
        /// 
        /// 1. **Cache Check**: Returns cached results for normalized query (fastest)
        /// 2. **Precomputed Suggestions**: Queries FuzzySuggestions table (fast indexed lookup)
        /// 3. **FTS Query**: Uses ProductFTS virtual table with MATCH operator (very fast)
        /// 4. **Fuzzy Fallback**: Scans candidates and scores with TokenSetRatio (comprehensive)
        /// 
        /// Results are cached for 5 minutes to avoid repeated work.
        /// Match positions are populated for UI highlighting in results.
        /// 
        /// Performance:
        /// - Empty query returns empty list immediately
        /// - Query normalized for case-insensitive matching
        /// - Results limited to top 10 by configuration
        /// </remarks>
        Task<List<SearchResultDto>> SearchNames(string q);
    }
}
