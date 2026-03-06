using Xunit;
using ELKH.Services;
using Microsoft.Extensions.Options;
using ELKH.Configuration;
using System.Linq;

namespace Elkh.Tests
{
    /// <summary>
    /// Unit tests for <see cref="FuzzyHelperService.FindBestMatchPositions"/>.
    /// Covers the fast exact-match path and the fuzzy sliding-window fallback.
    /// </summary>
    public class FuzzyHelperServiceTests
    {
        private FuzzyHelperService Create()
        {
            var opt = Options.Create(new SearchOptions());
            return new FuzzyHelperService(opt);
        }

        /// <summary>
        /// Exact prefix match ("red" in "Red Shirt Large") should be found via
        /// the fast <c>IndexOf</c> path and return the correct start/length.
        /// </summary>
        [Fact]
        public void FindBestMatchPositions_SimplePrefix_Matches()
        {
            var svc = Create();
            var tokens = new[] { "red" };
            var name = "Red Shirt Large";
            var pos = svc.FindBestMatchPositions(tokens, name);
            Assert.True(pos.Any());
            Assert.Equal("Red", name.Substring(pos[0].start, pos[0].length));
        }

        /// <summary>
        /// A one-character typo ("appl" vs "Apple") should still produce a match
        /// via the fuzzy sliding-window fallback.
        /// </summary>
        [Fact]
        public void FindBestMatchPositions_Typo_MatchesApproximately()
        {
            var svc = Create();
            var tokens = new[] { "appl" };
            var name = "Apple Juice";
            var pos = svc.FindBestMatchPositions(tokens, name);
            Assert.True(pos.Any());
        }
    }
}
