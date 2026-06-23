namespace Ciderfy.Tui;

/// <summary>
/// Parses TUI commands and exposes matching help/completion metadata
/// </summary>
internal static partial class TuiCommands
{
    private const int MaxSuggestions = 5;

    internal const char ArgumentSeparator = ' ';

    internal static IReadOnlyList<TuiCommandSuggestion> GetSuggestions(
        string input,
        bool awaitingUserToken
    )
    {
        if (awaitingUserToken || !input.StartsWith('/'))
        {
            return [];
        }

        return
        [
            .. _definitions
                .SelectMany(static d => d.Suggestions)
                .Where(s => s.Completion.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                .Take(MaxSuggestions),
        ];
    }

    internal static TuiCommand Parse(string raw)
    {
        var trimmed = raw.Trim();
        var parts = trimmed.Split(ArgumentSeparator, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return new TuiCommand(TuiCommandKind.Unknown, string.Empty, Argument: null);
        }

        var name = parts[0];
        var argument = parts.Length > 1 ? parts[1].Trim() : null;

        var definition = _definitions.SingleOrDefault(d => d.MatchesName(name));

        if (definition is null)
        {
            return new TuiCommand(TuiCommandKind.Unknown, name, Argument: null);
        }

        if (argument is not null && !definition.AllowsArguments)
        {
            return new TuiCommand(TuiCommandKind.Unknown, trimmed, Argument: null);
        }

        return new TuiCommand(definition.Kind, name, argument);
    }
}
