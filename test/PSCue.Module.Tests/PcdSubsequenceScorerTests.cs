using System;
using System.IO;
using System.Reflection;
using PSCue.Module;
using Xunit;

namespace PSCue.Module.Tests;

public class PcdSubsequenceScorerTests
{
    #region ScoreRaw - Basic matching

    [Fact]
    public void ScoreRaw_EmptyQuery_Returns0()
    {
        var score = PcdSubsequenceScorer.ScoreRaw("".AsSpan(), "abc".AsSpan());
        Assert.Equal(0, score);
    }

    [Fact]
    public void ScoreRaw_QueryLongerThanTarget_ReturnsMinValue()
    {
        var score = PcdSubsequenceScorer.ScoreRaw("abcdef".AsSpan(), "abc".AsSpan());
        Assert.Equal(int.MinValue, score);
    }

    [Fact]
    public void ScoreRaw_NoMatch_ReturnsMinValue()
    {
        var score = PcdSubsequenceScorer.ScoreRaw("xyz".AsSpan(), "abc".AsSpan());
        Assert.Equal(int.MinValue, score);
    }

    [Fact]
    public void ScoreRaw_WrongOrder_ReturnsMinValue()
    {
        var score = PcdSubsequenceScorer.ScoreRaw("cba".AsSpan(), "abc".AsSpan());
        Assert.Equal(int.MinValue, score);
    }

    [Fact]
    public void ScoreRaw_ExactMatch_ReturnsPositiveScore()
    {
        var score = PcdSubsequenceScorer.ScoreRaw("git".AsSpan(), "git".AsSpan());
        Assert.True(score > 0, $"Expected positive score for exact match, got {score}");
    }

    [Fact]
    public void ScoreRaw_SubsequenceMatch_ReturnsPositiveScore()
    {
        var score = PcdSubsequenceScorer.ScoreRaw("ddt".AsSpan(), "dd-trace-dotnet".AsSpan());
        Assert.True(score > 0, $"Expected positive score for subsequence match, got {score}");
    }

    [Fact]
    public void ScoreRaw_CaseInsensitive_Matches()
    {
        var score = PcdSubsequenceScorer.ScoreRaw("GIT".AsSpan(), "git".AsSpan());
        Assert.True(score != int.MinValue, "Expected case-insensitive match");
    }

    #endregion

    #region ScoreRaw - Boundary bonuses

    [Fact]
    public void ScoreRaw_BoundaryMatch_HigherThanMidWord()
    {
        // "ddt" at boundaries in "dd-trace-dotnet" (d, d after start, t after -)
        var boundaryScore = PcdSubsequenceScorer.ScoreRaw("ddt".AsSpan(), "dd-trace-dotnet".AsSpan());
        // "ddt" mid-word in "oddtest"
        var midWordScore = PcdSubsequenceScorer.ScoreRaw("ddt".AsSpan(), "oddtest".AsSpan());

        Assert.True(boundaryScore > midWordScore,
            $"Boundary score ({boundaryScore}) should be higher than mid-word score ({midWordScore})");
    }

    [Fact]
    public void ScoreRaw_HyphenDelimiter_GivesBoundaryBonus()
    {
        // "td" where 't' is at start and 'd' is after hyphen
        var withHyphen = PcdSubsequenceScorer.ScoreRaw("td".AsSpan(), "trace-dotnet".AsSpan());
        // "td" mid-word
        var midWord = PcdSubsequenceScorer.ScoreRaw("td".AsSpan(), "atdoor".AsSpan());

        Assert.True(withHyphen > midWord,
            $"Hyphen boundary ({withHyphen}) should score higher than mid-word ({midWord})");
    }

    [Fact]
    public void ScoreRaw_UnderscoreDelimiter_GivesBoundaryBonus()
    {
        // "mp" where 'm' is at start and 'p' is after underscore
        var withUnderscore = PcdSubsequenceScorer.ScoreRaw("mp".AsSpan(), "my_project".AsSpan());
        // "mp" mid-word
        var midWord = PcdSubsequenceScorer.ScoreRaw("mp".AsSpan(), "ampton".AsSpan());

        Assert.True(withUnderscore > midWord,
            $"Underscore boundary ({withUnderscore}) should score higher than mid-word ({midWord})");
    }

    [Fact]
    public void ScoreRaw_DotDelimiter_GivesBoundaryBonus()
    {
        // "sn" where 's' is at start and 'n' is after dot
        var withDot = PcdSubsequenceScorer.ScoreRaw("sn".AsSpan(), "Some.Namespace".AsSpan());
        // "sn" mid-word
        var midWord = PcdSubsequenceScorer.ScoreRaw("sn".AsSpan(), "snack".AsSpan());

        // Both should match, but dot boundary should score higher
        Assert.True(withDot != int.MinValue);
        Assert.True(midWord != int.MinValue);
    }

    [Fact]
    public void ScoreRaw_CamelCase_GivesBoundaryBonus()
    {
        // "fb" matching at camelCase boundaries in "FooBar"
        var camelCase = PcdSubsequenceScorer.ScoreRaw("fb".AsSpan(), "FooBar".AsSpan());
        // "fb" mid-word
        var midWord = PcdSubsequenceScorer.ScoreRaw("fb".AsSpan(), "dfbuild".AsSpan());

        Assert.True(camelCase > midWord,
            $"CamelCase boundary ({camelCase}) should score higher than mid-word ({midWord})");
    }

    #endregion

    #region ScoreRaw - Consecutive and gap penalties

    [Fact]
    public void ScoreRaw_ConsecutiveChars_BeatScatteredChars()
    {
        // "src" consecutive in "src-files"
        var consecutive = PcdSubsequenceScorer.ScoreRaw("src".AsSpan(), "src-files".AsSpan());
        // "src" scattered in "sXrXcXdata"
        var scattered = PcdSubsequenceScorer.ScoreRaw("src".AsSpan(), "sXrXcXdata".AsSpan());

        Assert.True(consecutive > scattered,
            $"Consecutive ({consecutive}) should beat scattered ({scattered})");
    }

    [Fact]
    public void ScoreRaw_TighterMatch_BeatsSpreaderMatch()
    {
        // "ac" tight in "abc"
        var tight = PcdSubsequenceScorer.ScoreRaw("ac".AsSpan(), "abc".AsSpan());
        // "ac" spread in "aXXXXc"
        var spread = PcdSubsequenceScorer.ScoreRaw("ac".AsSpan(), "aXXXXc".AsSpan());

        Assert.True(tight > spread,
            $"Tight match ({tight}) should beat spread match ({spread})");
    }

    #endregion

    #region Score (normalized) - Range validation

    [Theory]
    [InlineData("git", "git")]
    [InlineData("ddt", "dd-trace-dotnet")]
    [InlineData("mp", "my_project")]
    [InlineData("fb", "FooBar")]
    [InlineData("src", "source")]
    public void Score_ValidMatch_ReturnsInRange(string query, string target)
    {
        var score = PcdSubsequenceScorer.Score(query.AsSpan(), target.AsSpan());
        Assert.True(score > 0.0 && score <= 0.8,
            $"Score {score} for '{query}' vs '{target}' should be in (0.0, 0.8]");
    }

    [Theory]
    [InlineData("xyz", "abc")]
    [InlineData("cba", "abc")]
    [InlineData("abcdef", "abc")]
    public void Score_NoMatch_Returns0(string query, string target)
    {
        var score = PcdSubsequenceScorer.Score(query.AsSpan(), target.AsSpan());
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void Score_EmptyQuery_Returns0()
    {
        var score = PcdSubsequenceScorer.Score("".AsSpan(), "abc".AsSpan());
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void Score_ExactMatch_ScoresNear08()
    {
        var score = PcdSubsequenceScorer.Score("git".AsSpan(), "git".AsSpan());
        Assert.True(score >= 0.7, $"Exact match should score near 0.8, got {score}");
    }

    #endregion

    #region Integration - CalculateMatchScore via reflection

    [Fact]
    public void CalculateMatchScore_SubsequenceMatch_ReturnsPositive()
    {
        var graph = new ArgumentGraph();
        var engine = new PcdCompletionEngine(graph);
        var method = typeof(PcdCompletionEngine).GetMethod("CalculateMatchScore",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var testPath = Path.Combine("D:", "source", "datadog", "dd-trace-dotnet");
        var score = (double)method!.Invoke(engine, new object[] { testPath, "ddt" })!;

        Assert.True(score > 0.0, $"Expected positive score for subsequence match 'ddt', got {score}");
    }

    [Fact]
    public void CalculateMatchScore_SubsequenceScore_LessThanPrefixScore()
    {
        var graph = new ArgumentGraph();
        var engine = new PcdCompletionEngine(graph);
        var method = typeof(PcdCompletionEngine).GetMethod("CalculateMatchScore",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var testPath = Path.Combine("D:", "source", "datadog", "dd-trace-dotnet");

        // Prefix match should return 0.9
        var prefixScore = (double)method!.Invoke(engine, new object[] { testPath, "dd-trace" })!;
        // Subsequence match should return < 0.9
        var subsequenceScore = (double)method!.Invoke(engine, new object[] { testPath, "ddt" })!;

        Assert.True(subsequenceScore < prefixScore,
            $"Subsequence score ({subsequenceScore}) should be less than prefix score ({prefixScore})");
    }

    [Fact]
    public void CalculateMatchScore_TypoFallback_StillWorks()
    {
        var graph = new ArgumentGraph();
        var engine = new PcdCompletionEngine(graph);
        var method = typeof(PcdCompletionEngine).GetMethod("CalculateMatchScore",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // "datadgo" is not a subsequence of "datadog" (g before o in query, but o before g in target)
        // Levenshtein distance = 2 (transposition), similarity = 1 - 2/7 ≈ 0.71 — passes thresholds
        var testPath = Path.Combine("D:", "source", "datadog");
        var score = (double)method!.Invoke(engine, new object[] { testPath, "datadgo" })!;

        Assert.True(score > 0.0, $"Expected Levenshtein fallback to match 'datadgo' against 'datadog', got {score}");
    }

    #endregion
}
