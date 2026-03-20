using System;
using System.Linq;
using ELKH.Configuration;
using ELKH.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace ELKH.Tests;

/// <summary>
/// Unit tests for <see cref="FuzzyHelperService.FindBestMatchPositions"/>.
/// Covers the fast exact-match path and the fuzzy sliding-window fallback.
/// </summary>
public class FuzzyHelperServiceTests
{
    private static FuzzyHelperService Create()
    {
        var opt = Options.Create(new SearchOptions());
        return new FuzzyHelperService(opt);
    }

    /// <summary>
    /// Exact prefix match ("red" in "Red Shirt Large") should be found via
    /// the fast IndexOf path and return the correct start/length.
    /// </summary>
    [Fact]
    public void FindBestMatchPositions_SimplePrefix_Matches()
    {
        var svc    = Create();
        var pos    = svc.FindBestMatchPositions(new[] { "red" }, "Red Shirt Large");
        Assert.True(pos.Any());
        Assert.Equal("Red", "Red Shirt Large".Substring(pos[0].start, pos[0].length));
    }

    /// <summary>
    /// A one-character typo ("appl" vs "Apple") should still produce a match
    /// via the fuzzy sliding-window fallback.
    /// </summary>
    [Fact]
    public void FindBestMatchPositions_Typo_MatchesApproximately()
    {
        var svc = Create();
        var pos = svc.FindBestMatchPositions(new[] { "appl" }, "Apple Juice");
        Assert.True(pos.Any());
    }

    /// <summary>
    /// An empty token list should return no positions (not throw).
    /// </summary>
    [Fact]
    public void FindBestMatchPositions_EmptyTokens_ReturnsEmpty()
    {
        var svc = Create();
        var pos = svc.FindBestMatchPositions(Array.Empty<string>(), "Blue Sticker");
        Assert.Empty(pos);
    }

    /// <summary>
    /// A query that matches nothing should return an empty list (not throw).
    /// </summary>
    [Fact]
    public void FindBestMatchPositions_NoMatch_ReturnsEmpty()
    {
        var svc = Create();
        var pos = svc.FindBestMatchPositions(new[] { "zzzzzzzzz" }, "Apple Juice");
        Assert.Empty(pos);
    }
}
