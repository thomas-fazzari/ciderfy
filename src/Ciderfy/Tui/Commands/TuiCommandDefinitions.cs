namespace Ciderfy.Tui;

internal static partial class TuiCommands
{
    private static readonly CommandDefinition[] _definitions =
    [
        new(
            TuiCommandKind.Auth,
            "/auth",
            AllowsArguments: false,
            Aliases: [],
            HelpRows: [new("/auth", "Authenticate with Apple Music")],
            Suggestions: [new("/auth", "/auth", "Authenticate with Apple Music")]
        ),
        new(
            TuiCommandKind.AuthReset,
            "/reset-auth",
            AllowsArguments: false,
            Aliases: [],
            HelpRows: [new("/reset-auth", "Clear cached tokens and re-authenticate")],
            Suggestions:
            [
                new("/reset-auth", "/reset-auth", "Clear cached tokens and re-authenticate"),
            ]
        ),
        new(
            TuiCommandKind.Config,
            "/config",
            AllowsArguments: false,
            Aliases: ["/cfg"],
            HelpRows: [new("/config", "Open Ciderfy configuration folder")],
            Suggestions: [new("/config", "/config", "Open Ciderfy configuration folder")]
        ),
        new(
            TuiCommandKind.Status,
            "/status",
            AllowsArguments: false,
            Aliases: [],
            HelpRows: [new("/status", "Show authentication status")],
            Suggestions: [new("/status", "/status", "Show authentication status")]
        ),
        new(
            TuiCommandKind.Storefront,
            "/storefront",
            AllowsArguments: true,
            Aliases: ["/sf"],
            HelpRows: [new("/storefront <code>", "Set Apple Music storefront")],
            Suggestions: [new("/storefront ", "/storefront <code>", "Set Apple Music storefront")]
        ),
        new(
            TuiCommandKind.Name,
            "/name",
            AllowsArguments: true,
            Aliases: [],
            HelpRows:
            [
                new("/name <name>", "Set playlist name for next transfer"),
                new("/name", "Clear playlist name override"),
            ],
            Suggestions:
            [
                new("/name ", "/name <name>", "Set playlist name for next transfer"),
                new("/name", "/name", "Clear playlist name override"),
            ]
        ),
        new(
            TuiCommandKind.Add,
            "/add",
            AllowsArguments: true,
            Aliases: [],
            HelpRows: [new("/add <url>", "Queue Spotify playlists to merge")],
            Suggestions: [new("/add ", "/add <url>", "Queue Spotify playlists to merge")]
        ),
        new(
            TuiCommandKind.Run,
            "/run",
            AllowsArguments: false,
            Aliases: [],
            HelpRows: [new("/run", "Start queued playlist transfer")],
            Suggestions: [new("/run", "/run", "Start queued playlist transfer")]
        ),
        new(
            TuiCommandKind.Help,
            "/help",
            AllowsArguments: false,
            Aliases: ["/h"],
            HelpRows: [new("/help", "Show command help")],
            Suggestions: [new("/help", "/help", "Show command help")]
        ),
        new(
            TuiCommandKind.Quit,
            "/quit",
            AllowsArguments: false,
            Aliases: ["/exit", "/q"],
            HelpRows: [new("/quit", "Exit")],
            Suggestions: [new("/quit", "/quit", "Exit")]
        ),
    ];

    internal static readonly TuiCommandHelpEntry[] HelpEntries =
    [
        .. _definitions.SelectMany(static d => d.HelpRows),
        new(string.Empty, string.Empty),
        new("<spotify-url>", "Paste a Spotify playlist URL to transfer directly"),
    ];

    private sealed record CommandDefinition(
        TuiCommandKind Kind,
        string Command,
        bool AllowsArguments,
        string[] Aliases,
        TuiCommandHelpEntry[] HelpRows,
        TuiCommandSuggestion[] Suggestions
    )
    {
        internal bool MatchesName(string name) =>
            Command.Equals(name, StringComparison.OrdinalIgnoreCase)
            || Aliases.Contains(name, StringComparer.OrdinalIgnoreCase);
    }
}
