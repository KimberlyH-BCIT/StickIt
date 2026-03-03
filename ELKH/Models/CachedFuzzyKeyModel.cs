using System.ComponentModel.DataAnnotations;

namespace ELKH.Models
{
    /// <summary>
    /// Represents a persisted cache key for fuzzy search results.
    /// Used to track and clear cached fuzzy search entries.
    /// </summary>
    public class CachedFuzzyKeyModel
    {
        /// <summary>
        /// Unique identifier for the cache key (primary key).
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The cache key string used for lookup in the memory cache.
        /// </summary>
        public string CacheKey { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the cache key was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
