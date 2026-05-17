namespace Ciderfy.Tui;

internal static partial class TuiCommands
{
    /// <summary>
    /// Command to start Apple Music authentication process
    /// </summary>
    private const string Auth = "/auth";

    /// <summary>
    /// Command to clear cached tokens, then authenticate</summary>
    private const string ResetAuth = "/reset-auth";

    /// <summary>
    /// Command to show token and storefront status
    /// </summary>
    private const string Status = "/status";

    /// <summary>
    /// Command to open Ciderfy configuration folder</summary>
    private const string Config = "/config";

    /// <summary>
    /// Short alias for <see cref="Config" />
    /// </summary>
    private const string ConfigShort = "/cfg";

    /// <summary>
    /// Command to set Apple Music storefront
    /// </summary>
    private const string Storefront = "/storefront";

    /// <summary>
    /// Short alias for <see cref="Storefront" />
    /// </summary>
    private const string StorefrontShort = "/sf";

    /// <summary>
    /// Command to set the next transfered playlist name
    /// </summary>
    private const string Name = "/name";

    /// <summary>
    /// Command to queue Spotify playlist URLs
    /// </summary>
    private const string Add = "/add";

    /// <summary>
    /// Command to start a transfer for queued playlists
    /// </summary>
    private const string Run = "/run";

    /// <summary>
    /// Command to show command help
    /// </summary>
    private const string Help = "/help";

    /// <summary>
    /// Short alias for <see cref="Help" />
    /// </summary>
    private const string HelpShort = "/h";

    /// <summary>
    /// Command to exit the TUI
    /// </summary>
    private const string Quit = "/quit";

    /// <summary>
    /// Short alias for <see cref="Quit" />
    /// </summary>
    private const string QuitShort = "/q";

    /// <summary>
    /// Alias for <see cref="Quit" />
    /// </summary>
    private const string Exit = "/exit";

    private static readonly CommandDefinition[] _definitions =
    [
        new(
            TuiCommandKind.Auth,
            Auth,
            AllowsArguments: false,
            Aliases: [],
            HelpRows: [new(Auth, "Authenticate with Apple Music")],
            Suggestions: [new(Auth, Auth, "Authenticate with Apple Music")]
        ),
        new(
            TuiCommandKind.AuthReset,
            ResetAuth,
            AllowsArguments: false,
            Aliases: [],
            HelpRows: [new(ResetAuth, "Clear cached tokens and re-authenticate")],
            Suggestions: [new(ResetAuth, ResetAuth, "Clear cached tokens and re-authenticate")]
        ),
        new(
            TuiCommandKind.Config,
            Config,
            AllowsArguments: false,
            Aliases: [ConfigShort],
            HelpRows: [new(Config, "Open Ciderfy configuration folder")],
            Suggestions: [new(Config, Config, "Open Ciderfy configuration folder")]
        ),
        new(
            TuiCommandKind.Status,
            Status,
            AllowsArguments: false,
            Aliases: [],
            HelpRows: [new(Status, "Show authentication status")],
            Suggestions: [new(Status, Status, "Show authentication status")]
        ),
        new(
            TuiCommandKind.Storefront,
            Storefront,
            AllowsArguments: true,
            Aliases: [StorefrontShort],
            HelpRows: [new(Storefront + " <code>", "Set Apple Music storefront")],
            Suggestions:
            [
                new(
                    Storefront + ArgumentSeparator,
                    Storefront + " <code>",
                    "Set Apple Music storefront"
                ),
            ]
        ),
        new(
            TuiCommandKind.Name,
            Name,
            AllowsArguments: true,
            Aliases: [],
            HelpRows:
            [
                new(Name + " <name>", "Set playlist name for next transfer"),
                new(Name, "Clear playlist name override"),
            ],
            Suggestions:
            [
                new(
                    Name + ArgumentSeparator,
                    Name + " <name>",
                    "Set playlist name for next transfer"
                ),
                new(Name, Name, "Clear playlist name override"),
            ]
        ),
        new(
            TuiCommandKind.Add,
            Add,
            AllowsArguments: true,
            Aliases: [],
            HelpRows: [new(Add + " <url>", "Queue Spotify playlists to merge")],
            Suggestions:
            [
                new(Add + ArgumentSeparator, Add + " <url>", "Queue Spotify playlists to merge"),
            ]
        ),
        new(
            TuiCommandKind.Run,
            Run,
            AllowsArguments: false,
            Aliases: [],
            HelpRows: [new(Run, "Start queued playlist transfer")],
            Suggestions: [new(Run, Run, "Start queued playlist transfer")]
        ),
        new(
            TuiCommandKind.Help,
            Help,
            AllowsArguments: false,
            Aliases: [HelpShort],
            HelpRows: [new(Help, "Show command help")],
            Suggestions: [new(Help, Help, "Show command help")]
        ),
        new(
            TuiCommandKind.Quit,
            Quit,
            AllowsArguments: false,
            Aliases: [Exit, QuitShort],
            HelpRows: [new(Quit, "Exit")],
            Suggestions: [new(Quit, Quit, "Exit")]
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
