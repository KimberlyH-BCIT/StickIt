using Xunit;
using ELKH.Services;
using Microsoft.Extensions.Options;
using ELKH.Configuration;
using System.Linq;

namespace Elkh.Tests
{
    public class FuzzyHelperServiceTests
    {
        private FuzzyHelperService Create()
        {
            var opt = Options.Create(new SearchOptions());
            return new FuzzyHelperService(opt);
        }

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
