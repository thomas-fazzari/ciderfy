namespace Ciderfy.Tui;

internal sealed record TuiCommandHelpEntry(string Usage, string Description);

internal sealed record TuiCommandSuggestion(string Completion, string Usage, string Description);

internal static class TuiCommands
{
    private const int MaxSuggestions = 5;

    internal const string Add = "/add";
    internal const string Auth = "/auth";
    internal const string AuthReset = "/auth reset";
    internal const string Config = "/config";
    internal const string ConfigShort = "/cfg";
    internal const string Exit = "/exit";
    internal const string Help = "/help";
    internal const string HelpShort = "/h";
    internal const string Name = "/name";
    internal const string Quit = "/quit";
    internal const string QuitShort = "/q";
    internal const string Run = "/run";
    internal const string Status = "/status";
    internal const string Storefront = "/storefront";
    internal const string StorefrontShort = "/sf";

    internal static readonly TuiCommandSuggestion[] Suggestions =
    [
        new(Auth, Auth, "Authenticate with Apple Music"),
        new(AuthReset, AuthReset, "Clear cached tokens and re-authenticate"),
        new(Config, Config, "Open Ciderfy configuration folder"),
        new(Status, Status, "Show authentication status"),
        new(Storefront + " ", Storefront + " <code>", "Set Apple Music storefront"),
        new(Name + " ", Name + " <name>", "Set playlist name for next transfer"),
        new(Name, Name, "Clear playlist name override"),
        new(Add + " ", Add + " <url>", "Queue Spotify playlists to merge"),
        new(Run, Run, "Start queued playlist transfer"),
        new(Help, Help, "Show command help"),
        new(Quit, Quit, "Exit"),
    ];

    internal static readonly TuiCommandHelpEntry[] HelpEntries =
    [
        new(Auth, "Authenticate with Apple Music"),
        new(AuthReset, "Clear cached tokens and re-authenticate"),
        new(Config, "Open Ciderfy configuration folder"),
        new(Status, "Show authentication status"),
        new(Storefront + " <code>", "Set Apple Music storefront (default: us)"),
        new(Name + " <name>", "Set playlist name for next transfer"),
        new(Name, "Clear playlist name override"),
        new(Add + " <url>", "Queue a Spotify playlist to merge multiple"),
        new(Run, "Start transferring the queued playlists"),
        new(Help, "Show this help"),
        new(Quit, "Exit"),
        new(string.Empty, string.Empty),
        new("<spotify-url>", "Paste a Spotify playlist URL to transfer directly"),
    ];

    internal static IReadOnlyList<TuiCommandSuggestion> GetSuggestions(
        string input,
        bool awaitingUserToken
    )
    {
        if (awaitingUserToken || !input.StartsWith('/'))
            return [];

        return Suggestions
            .Where(s => s.Completion.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSuggestions)
            .ToArray();
    }
}
