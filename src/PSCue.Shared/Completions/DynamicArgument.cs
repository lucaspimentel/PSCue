using System.Diagnostics;

namespace PSCue.Shared.Completions;

[DebuggerDisplay("{CompletionText,nq}")]
public sealed class DynamicArgument(string completionText, string? tooltip = null)
    : ICompletion
{
    public string CompletionText { get; } = completionText;
    public string? Tooltip { get; } = tooltip;

    public ICompletion? FindNode(ReadOnlySpan<char> wordToComplete)
    {
        return Helpers.ThisIfEquals(this, wordToComplete);
    }

    public override string ToString()
    {
        return CompletionText;
    }
}
