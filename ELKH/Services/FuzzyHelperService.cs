using System;
using System.Collections.Generic;
using ELKH.Configuration;
using FuzzySharp;
using Microsoft.Extensions.Options;

namespace ELKH.Services
{
    /// <summary>
    /// Utility service for fuzzy string matching and match-position computation.
    /// Used by <see cref="SearchService"/> to score search candidates and locate
    /// the character ranges that should be highlighted in the UI autocomplete dropdown.
    /// </summary>
    public class FuzzyHelperService
    {
        private readonly SearchOptions _options;

        /// <summary>
        /// Initializes a new instance of <see cref="FuzzyHelperService"/>.
        /// </summary>
        /// <param name="options">Search options containing fuzzy window deltas and partial-ratio threshold.</param>
        public FuzzyHelperService(IOptions<SearchOptions> options)
        {
            _options = options?.Value ?? new SearchOptions();
        }

        /// <summary>
        /// Compute a raw partial ratio score between two strings using FuzzySharp.
        /// This method is a thin wrapper so callers are explicit about intent.
        /// </summary>
        public int PartialRatio(string a, string b) => Fuzz.PartialRatio(a, b);

        /// <summary>
        /// Find the best matching substring positions for the given search tokens inside a target name.
        ///
        /// This method attempts a fast exact substring search first. If that fails it performs
        /// a sliding-window fuzzy search across varying window sizes. The window sizes are
        /// determined by the token length plus configuration deltas to allow flexible matching
        /// for shorter/longer candidate substrings.
        ///
        /// Returns a list of (start, length) tuples describing regions in <paramref name="name"/>
        /// that best match each token and meet the configured partial ratio threshold.
        /// </summary>
        public List<(int start, int length)> FindBestMatchPositions(string[] tokens, string name)
        {
            var outList = new List<(int start, int length)>();
            if (tokens == null || tokens.Length == 0 || string.IsNullOrEmpty(name)) return outList;

            // Normalize the target string once for case-insensitive matching.
            var nameLower = name.ToLowerInvariant();

            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token)) continue;

                // Normalize the token for comparison.
                var t = token.ToLowerInvariant();

                // Fast path: direct substring match. If found, record and continue to next token.
                var idx = nameLower.IndexOf(t, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    outList.Add((idx, t.Length));
                    continue;
                }

                // Fuzzy fallback: search across a range of window sizes around token length.
                // This provides tolerance for insertion/deletion differences between token and segments.
                var cfg = _options.Fuzzy;
                var bestIdx = -1; var bestLen = 0; var bestScore = -1;

                // Determine bounds for the sliding window. Ensure min length is at least 1
                // and max length doesn't exceed the target name length.
                var minLen = Math.Max(1, t.Length + cfg.WindowMinDelta);
                var maxLen = Math.Min(nameLower.Length, t.Length + cfg.WindowMaxDelta);

                // Iterate window sizes and starting positions to find the substring with the
                // highest partial ratio score against the token.
                for (var len = minLen; len <= maxLen; len++)
                {
                    for (int i = 0; i + len <= nameLower.Length; i++)
                    {
                        var sub = nameLower.Substring(i, len);
                        var score = Fuzz.PartialRatio(t, sub);

                        // Record if this candidate improves the best score so far.
                        if (score > bestScore)
                        {
                            bestScore = score; bestIdx = i; bestLen = len;
                            // Perfect match short-circuits remaining work for efficiency.
                            if (bestScore == 100) break;
                        }
                    }
                    if (bestScore == 100) break;
                }

                // Only accept the fuzzy match if it meets the configured threshold.
                if (bestIdx >= 0 && bestScore >= _options.Fuzzy.PartialRatioThreshold)
                {
                    outList.Add((bestIdx, bestLen));
                }
            }

            return outList;
        }
    }
}
