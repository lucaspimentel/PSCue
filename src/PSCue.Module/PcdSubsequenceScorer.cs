using System;
using System.IO;

namespace PSCue.Module;

/// <summary>
/// Subsequence scorer for PCD directory matching, adapted from Wade's FuzzyScorer.
/// Uses greedy forward+backward scanning with boundary-aware scoring.
/// Returns normalized scores (0.0-0.8) to fit PCD's tiered scoring system
/// where exact matches = 1.0 and prefix matches = 0.9.
/// </summary>
internal static class PcdSubsequenceScorer
{
    // Scoring constants (fzf-inspired, same as Wade's FuzzyScorer)
    internal const int ScoreMatch = 16;
    internal const int PenaltyGapStart = -3;
    internal const int PenaltyGapExtension = -1;
    internal const int BonusBoundary = 8;
    internal const int BonusBoundaryDelimiter = 9;
    internal const int BonusCamel = 7;
    internal const int BonusNonWord = 8;
    internal const int BonusConsecutive = 4;
    internal const int BonusFirstCharMultiplier = 2;
    internal const int BonusCaseMatch = 1;
    internal const int PenaltyTrailingGap = -1;

    /// <summary>
    /// Score a query against a target string using subsequence matching.
    /// Returns 0.0 if no subsequence match found, otherwise a normalized score in (0.0, 0.8].
    /// </summary>
    internal static double Score(ReadOnlySpan<char> query, ReadOnlySpan<char> target)
    {
        int rawScore = ScoreRaw(query, target);

        if (rawScore == int.MinValue)
            return 0.0;

        return Normalize(rawScore, query.Length, target.Length);
    }

    /// <summary>
    /// Raw integer score for testing/debugging.
    /// Returns int.MinValue if no subsequence match found.
    /// </summary>
    internal static int ScoreRaw(ReadOnlySpan<char> query, ReadOnlySpan<char> target)
    {
        int queryLen = query.Length;
        int targetLen = target.Length;

        if (queryLen == 0)
            return 0;

        if (queryLen > targetLen)
            return int.MinValue;

        // Pre-lowercase the query once, normalizing path separators.
        Span<char> queryLower = queryLen <= 64 ? stackalloc char[queryLen] : new char[queryLen];
        for (int i = 0; i < queryLen; i++)
        {
            char c = query[i];
            queryLower[i] = c is '/' or '\\'
                ? char.ToLowerInvariant(Path.DirectorySeparatorChar)
                : char.ToLowerInvariant(c);
        }

        // Forward scan: greedily find the first subsequence match.
        Span<int> forwardPositions = queryLen <= 64 ? stackalloc int[queryLen] : new int[queryLen];
        int qi = 0;

        for (int ti = 0; ti < targetLen && qi < queryLen; ti++)
        {
            if (char.ToLowerInvariant(target[ti]) == queryLower[qi])
            {
                forwardPositions[qi] = ti;
                qi++;
            }
        }

        if (qi < queryLen)
            return int.MinValue; // Not all query chars found.

        // Backward scan: from the last forward match, walk backward to tighten the match span.
        Span<int> matchPositions = queryLen <= 64 ? stackalloc int[queryLen] : new int[queryLen];
        int lastForwardPos = forwardPositions[queryLen - 1];
        qi = queryLen - 1;

        for (int ti = lastForwardPos; ti >= 0 && qi >= 0; ti--)
        {
            if (char.ToLowerInvariant(target[ti]) == queryLower[qi])
            {
                matchPositions[qi] = ti;
                qi--;
            }
        }

        return ComputeScore(query, target, matchPositions);
    }

    private static int ComputeScore(ReadOnlySpan<char> query, ReadOnlySpan<char> target, ReadOnlySpan<int> matchPositions)
    {
        int score = 0;
        int prevPosition = -1;
        int prevBonus = 0;

        for (int qi = 0; qi < matchPositions.Length; qi++)
        {
            int pos = matchPositions[qi];

            // Base match score.
            score += ScoreMatch;

            // Case-sensitive bonus (treat '/' and '\' as equivalent).
            char qc = query[qi];
            char tc = target[pos];
            if (qc == tc || (qc is '/' or '\\' && tc is '/' or '\\'))
            {
                score += BonusCaseMatch;
            }

            // Boundary bonus for this position.
            int bonus = GetBoundaryBonus(target, pos);

            // Consecutive match bonus: if this match immediately follows the previous one,
            // use the higher of the current bonus, the previous bonus, or the minimum consecutive bonus.
            if (prevPosition >= 0 && pos == prevPosition + 1)
            {
                bonus = Math.Max(bonus, Math.Max(prevBonus, BonusConsecutive));
            }

            // First character multiplier.
            if (qi == 0)
            {
                bonus *= BonusFirstCharMultiplier;
            }

            score += bonus;

            // Gap penalty.
            if (prevPosition >= 0)
            {
                int gap = pos - prevPosition - 1;

                if (gap > 0)
                {
                    score += PenaltyGapStart + PenaltyGapExtension * (gap - 1);
                }
            }
            else if (pos > 0)
            {
                // Gap before the first match.
                score += PenaltyGapStart + PenaltyGapExtension * (pos - 1);
            }

            prevPosition = pos;
            prevBonus = bonus;
        }

        // Trailing gap penalty: penalize unmatched characters after the last match.
        if (prevPosition >= 0)
        {
            int trailingGap = target.Length - prevPosition - 1;
            score += PenaltyTrailingGap * trailingGap;
        }

        return score;
    }

    private static int GetBoundaryBonus(ReadOnlySpan<char> target, int position)
    {
        if (position == 0)
        {
            // Start of string treated as delimiter boundary.
            return BonusBoundaryDelimiter;
        }

        char prev = target[position - 1];
        char curr = target[position];

        if (IsDelimiter(prev))
        {
            return BonusBoundaryDelimiter;
        }

        if (IsNonWord(prev) && IsWord(curr))
        {
            return BonusBoundary;
        }

        if (char.IsLower(prev) && char.IsUpper(curr))
        {
            return BonusCamel;
        }

        if (!char.IsDigit(prev) && char.IsDigit(curr))
        {
            return BonusCamel;
        }

        if (IsNonWord(curr))
        {
            return BonusNonWord;
        }

        return 0;
    }

    /// <summary>
    /// Checks if a character is a delimiter. Includes path separators plus common
    /// directory name separators (hyphen, underscore, dot) for boundary detection.
    /// </summary>
    private static bool IsDelimiter(char c) =>
        c == Path.DirectorySeparatorChar ||
        c == Path.AltDirectorySeparatorChar ||
        c == '-' ||
        c == '_' ||
        c == '.';

    private static bool IsNonWord(char c) =>
        !char.IsLetterOrDigit(c);

    private static bool IsWord(char c) =>
        char.IsLetterOrDigit(c);

    /// <summary>
    /// Normalize a raw score to the 0.0-0.8 range.
    /// Uses the theoretical maximum score for a perfect match of the given query length.
    /// </summary>
    private static double Normalize(int rawScore, int queryLength, int targetLength)
    {
        if (queryLength == 0)
            return 0.0;

        // Theoretical max: all chars consecutive, all at delimiter boundaries, exact case.
        // First char: ScoreMatch + BonusCaseMatch + BonusBoundaryDelimiter * BonusFirstCharMultiplier
        // Remaining: ScoreMatch + BonusCaseMatch + max(BonusConsecutive, BonusBoundaryDelimiter) each
        int bestBonus = Math.Max(BonusConsecutive, BonusBoundaryDelimiter);
        int maxScore = (ScoreMatch + BonusCaseMatch) * queryLength
                     + BonusBoundaryDelimiter * BonusFirstCharMultiplier
                     + bestBonus * (queryLength - 1);

        if (maxScore <= 0)
            return 0.0;

        double normalized = (double)rawScore / maxScore;
        return Math.Clamp(normalized, 0.0, 1.0) * 0.8;
    }
}
