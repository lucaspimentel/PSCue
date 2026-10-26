namespace PSCue.Shared.Completions;

/// <summary>
/// Materializes a completion node's <see cref="DynamicArgumentsFactory"/> result once per
/// request. The factory is an iterator that spawns child processes, so running it once per
/// call site doubles the cost of a request. CommandCompleter rebuilds the completion tree on
/// every request, so an instance never outlives a request and cannot serve stale data. Do
/// not start caching the completion tree without replacing this memo.
/// </summary>
internal sealed class DynamicArgumentsMemo
{
    private IReadOnlyList<DynamicArgument>? _arguments;

    /// <summary>
    /// Returns the materialized dynamic arguments, or null when the node has no factory. The
    /// first call evaluates the factory; later calls return the same list.
    /// </summary>
    public IReadOnlyList<DynamicArgument>? Get(DynamicArgumentsFactory? factory)
    {
        if (factory is null)
        {
            return null;
        }

        if (_arguments is null)
        {
            _arguments = factory.Invoke() is { } arguments ? [.. arguments] : [];
        }

        return _arguments;
    }
}
