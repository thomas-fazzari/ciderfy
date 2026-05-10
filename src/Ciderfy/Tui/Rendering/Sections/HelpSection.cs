using Spectre.Console;

namespace Ciderfy.Tui;

internal static class HelpSection
{
    private static readonly (string Command, string Description)[] _entries =
    [
        ($"[{Theme.Primary}]/auth[/]", "Authenticate with Apple Music"),
        ($"[{Theme.Primary}]/auth reset[/]", "Clear cached tokens and re-authenticate"),
        ($"[{Theme.Primary}]/config[/]", "Open Ciderfy configuration folder"),
        ($"[{Theme.Primary}]/status[/]", "Show authentication status"),
        (
            $"[{Theme.Primary}]/storefront[/] [{Theme.Muted}]<code>[/]",
            "Set Apple Music storefront (default: us)"
        ),
        (
            $"[{Theme.Primary}]/name[/] [{Theme.Muted}]<name>[/]",
            "Set playlist name for next transfer"
        ),
        ($"[{Theme.Primary}]/name[/]", "Clear playlist name override"),
        (
            $"[{Theme.Primary}]/add[/] [{Theme.Muted}]<url>[/]",
            "Queue a Spotify playlist to merge multiple"
        ),
        ($"[{Theme.Primary}]/run[/]", "Start transferring the queued playlists"),
        ($"[{Theme.Primary}]/help[/]", "Show this help"),
        ($"[{Theme.Primary}]/quit[/]", "Exit"),
        (string.Empty, string.Empty),
        ($"[{Theme.Primary}]<spotify-url>[/]", "Paste a Spotify playlist URL to transfer directly"),
    ];

    internal static int EntryCount => _entries.Length;

    internal static Table RenderTable(int scrollOffset, int visibleRows)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Theme.PrimaryColor)
            .AddColumn(new TableColumn("[bold]Command[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Description[/]"));

        var start = Math.Max(0, Math.Min(scrollOffset, Math.Max(0, _entries.Length - visibleRows)));
        var end = Math.Min(_entries.Length, start + visibleRows);

        for (var i = start; i < end; i++)
        {
            var entry = _entries[i];
            table.AddRow(entry.Command, entry.Description);
        }

        return table;
    }

    internal static Markup RenderScrollHint(int offset, int total, int visible)
    {
        if (total <= visible)
            return new Markup(string.Empty);

        return new Markup(
            $"[{Theme.Muted}]{Theme.ArrowUp}/{Theme.ArrowDown} scroll[/]{RenderHelpers.BuildScrollPositionInfo(offset, total, visible)}"
        );
    }
}
