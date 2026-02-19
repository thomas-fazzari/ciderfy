namespace Ciderfy.Tui;

internal sealed class TuiCommandRegistry
{
    private readonly Dictionary<string, Action<string?>> _handlers = new(
        StringComparer.OrdinalIgnoreCase
    );

    internal void Register(Action<string?> handler, params string[] aliases)
    {
        foreach (var alias in aliases)
            _handlers[alias] = handler;
    }

    internal bool TryExecute(string command, string? argument)
    {
        if (!_handlers.TryGetValue(command, out var handler))
            return false;

        handler(argument);
        return true;
    }
}
