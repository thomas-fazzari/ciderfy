using Ciderfy.Matching;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Ciderfy.Tui;

/// <summary>
/// Builds Spectre.Console renderables for each visual section of the TUI
/// </summary>
internal static class Components
{
    private static readonly string[] _bannerLines =
    [
        " ██████╗██╗██████╗ ███████╗██████╗ ███████╗██╗   ██╗",
        "██╔════╝██║██╔══██╗██╔════╝██╔══██╗██╔════╝╚██╗ ██╔╝",
        "██║     ██║██║  ██║█████╗  ██████╔╝█████╗   ╚████╔╝ ",
        "██║     ██║██║  ██║██╔══╝  ██╔══██╗██╔══╝    ╚██╔╝  ",
        "╚██████╗██║██████╔╝███████╗██║  ██║██║        ██║   ",
        " ╚═════╝╚═╝╚═════╝ ╚══════╝╚═╝  ╚═╝╚═╝        ╚═╝   ",
    ];

    internal static IRenderable RenderBanner(int width)
    {
        var normalizedLines = _bannerLines.Select(static line => line.TrimEnd()).ToArray();
        var bannerWidth = normalizedLines.Max(static line => line.Length);

        if (width < bannerWidth)
        {
            const string compactBanner = "CIDERFY";
            var compactPadding = Math.Max(0, (width - compactBanner.Length) / 2);
            return new Markup(
                $"[{Theme.Primary} bold]{new string(' ', compactPadding)}{compactBanner}[/]"
            );
        }

        var leftPadding = Math.Max(0, (width - bannerWidth) / 2);
        var rows = new List<IRenderable>(normalizedLines.Length);

        for (var i = 0; i < normalizedLines.Length; i++)
        {
            var color = Theme.BannerColors[i];
            var bold = i is 2 or 3 ? " bold" : "";
            var centeredLine = string.Concat(new string(' ', leftPadding), normalizedLines[i]);
            rows.Add(new Markup($"[{color}{bold}]{Markup.Escape(centeredLine)}[/]"));
        }

        return new Rows(rows);
    }

    internal static IRenderable RenderStatusBadges(
        bool hasValidDeveloperToken,
        bool hasValidUserToken,
        string storefront,
        string? nextPlaylistName
    )
    {
        var devBadge = hasValidDeveloperToken
            ? Badge("Developer valid", Theme.BadgeGoodFg, Theme.BadgeGoodBg)
            : Badge("Developer missing", Theme.BadgeBadFg, Theme.BadgeBadBg);

        var userBadge = hasValidUserToken
            ? Badge("User valid", Theme.BadgeGoodFg, Theme.BadgeGoodBg)
            : Badge("User missing", Theme.BadgeBadFg, Theme.BadgeBadBg);

        var sfBadge = Badge(
            $"Storefront {storefront.ToUpperInvariant()}",
            Theme.BadgeNeutralFg,
            Theme.BadgeNeutralBg
        );
        var nameBadge = string.IsNullOrWhiteSpace(nextPlaylistName)
            ? Badge("Name auto", Theme.BadgeNeutralFg, Theme.BadgeNeutralBg)
            : Badge("Name custom", Theme.BadgeNeutralFg, Theme.BadgeNeutralBg);

        return Align.Center(new Markup($"{devBadge} {userBadge} {sfBadge} {nameBadge}"));
    }

    private static string Badge(string text, string fg, string bg) =>
        $"[{fg} on {bg}] {Markup.Escape(text)} [/]";

    internal static IRenderable RenderLogArea(LogBuffer logs, int width, int height)
    {
        var visible = logs.GetVisible(height);
        var lines = new List<string>(height);

        foreach (var entry in visible)
        {
            var escaped = Markup.Escape(Truncate(entry.Message, width));
            lines.Add(
                entry.Kind switch
                {
                    LogKind.Success => $"[{Theme.Green}]{escaped}[/]",
                    LogKind.Warning => $"[{Theme.Yellow}]{escaped}[/]",
                    LogKind.Error => $"[{Theme.Red}]{escaped}[/]",
                    _ => $"[{Theme.Teal}]{escaped}[/]",
                }
            );
        }

        if (lines.Count == 0)
            lines.Add($"[{Theme.Muted}]No activity yet[/]");

        while (lines.Count < height)
            lines.Add("");

        return new Markup(string.Join('\n', lines));
    }

    internal static IRenderable RenderProgressBar(string label, double percent, int width)
    {
        var barWidth = Math.Max(10, width - 10);
        var filled = (int)(barWidth * Math.Clamp(percent, 0, 1));
        var empty = barWidth - filled;

        var bar = new string('\u2588', filled) + new string('\u2591', empty);
        var pctText = $" {(int)(percent * 100)}%";

        return new Markup(
            $"[{Theme.Teal}]{Markup.Escape(label)}[/]\n"
                + $"[{Theme.Primary}]{bar}[/]"
                + $"[{Theme.Muted}]{pctText}[/]"
        );
    }

    internal static IRenderable RenderSpinnerLine(string label, int spinnerTick)
    {
        var frame = Theme.SpinnerFrames[spinnerTick % Theme.SpinnerFrames.Length];
        return new Markup($"[{Theme.Primary}]{frame}[/] [{Theme.Teal}]{Markup.Escape(label)}[/]");
    }

    internal static IRenderable RenderConfirmPrompt(int count) =>
        new Markup(
            $"[{Theme.White} bold]"
                + $"Try text matching for {count} remaining track(s)? (can be slow) [[Y/n]] [/]"
        );

    internal static IRenderable RenderInput(string buffer, bool showCursor, int width)
    {
        var prompt = $"[{Theme.Teal} bold]> [/]";
        var text = string.IsNullOrEmpty(buffer)
            ? $"[{Theme.Muted}]Paste Spotify playlist URL or type /help[/]"
            : $"[{Theme.White}]{Markup.Escape(buffer)}[/]";

        var cursor = showCursor ? $"[{Theme.Teal}]\u2588[/]" : "";

        return new Panel(new Markup(prompt + text + cursor))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Theme.GrayColor),
            Padding = new Padding(1, 0),
            Width = width,
        };
    }

    internal static IRenderable RenderInputWithPrompt(
        string prompt,
        string buffer,
        bool showCursor,
        int width
    )
    {
        var promptMarkup = $"[{Theme.Teal} bold]{Markup.Escape(prompt)} [/]";
        var text = $"[{Theme.White}]{Markup.Escape(buffer)}[/]";
        var cursor = showCursor ? $"[{Theme.Teal}]\u2588[/]" : "";

        return new Panel(new Markup(promptMarkup + text + cursor))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Theme.GrayColor),
            Padding = new Padding(1, 0),
            Width = width,
        };
    }

    internal static IRenderable RenderResultsTable(
        List<MatchResult> allResults,
        int scrollOffset,
        int visibleRows,
        int width
    )
    {
        const int tableChromeWidth = 10;
        var indexWidth = Math.Max(2, allResults.Count.ToString().Length);
        var contentWidth = Math.Max(18, width - tableChromeWidth);
        var statusWidth = Math.Clamp(contentWidth / 3, 14, 26);
        var trackWidth = Math.Max(12, contentWidth - indexWidth - statusWidth);

        var table = new Table();
        table.Border(TableBorder.Simple);
        table.BorderColor(Theme.GrayColor);
        table.AddColumn(new TableColumn("[bold]#[/]") { Width = indexWidth }.NoWrap());
        table.AddColumn(new TableColumn("[bold]Track[/]") { Width = trackWidth }.NoWrap());
        table.AddColumn(new TableColumn("[bold]Status[/]") { Width = statusWidth }.NoWrap());

        var start = Math.Max(0, scrollOffset);
        var end = Math.Min(allResults.Count, start + visibleRows);

        for (var i = start; i < end; i++)
        {
            var r = allResults[i];
            var trackLabel = Markup.Escape(
                Truncate($"{r.SpotifyTrack.Artist} - {r.SpotifyTrack.Title}", trackWidth)
            );

            switch (r)
            {
                case MatchResult.Matched m:
                    var detail = m.Method == "ISRC" ? "ISRC" : $"Text {m.Confidence:F2}";
                    table.AddRow(
                        $"{i + 1}",
                        trackLabel,
                        $"[{Theme.Accent}]{Markup.Escape(Truncate(detail, statusWidth))}[/]"
                    );
                    break;
                case MatchResult.NotFound nf:
                    var reason = nf.Reason.StartsWith(
                        "Best match below threshold",
                        StringComparison.OrdinalIgnoreCase
                    )
                        ? $"Below threshold{nf.Reason["Best match below threshold".Length..]}"
                        : nf.Reason;
                    table.AddRow(
                        $"{i + 1}",
                        trackLabel,
                        $"[{Theme.Red}]{Markup.Escape(Truncate(reason, statusWidth))}[/]"
                    );
                    break;
            }
        }

        return table;
    }

    internal static IRenderable RenderSummaryPanel(
        string name,
        int matched,
        int total,
        int notFound
    )
    {
        var success = matched > 0;
        var content =
            $"[{Theme.Primary}]{matched}[/][{Theme.Muted}]/{total} tracks transferred[/]"
            + (notFound > 0 ? $"  [{Theme.Red}]{notFound} not found[/]" : "");

        var header = success ? $" {Markup.Escape(Truncate(name, 48))} " : " Transfer failed ";
        var borderColor = success ? Theme.PrimaryColor : Theme.RedColor;

        return new Panel(new Markup(content))
        {
            Header = new PanelHeader(header),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor),
            Padding = new Padding(2, 0),
        };
    }

    internal static IRenderable RenderHelpTable()
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.BorderColor(Theme.PrimaryDarkColor);
        table.AddColumn(new TableColumn($"[bold]Command[/]").NoWrap());
        table.AddColumn(new TableColumn("[bold]Description[/]"));

        table.AddRow($"[{Theme.Primary}]/auth[/]", "Authenticate with Apple Music");
        table.AddRow($"[{Theme.Primary}]/auth reset[/]", "Clear cached tokens and re-authenticate");
        table.AddRow($"[{Theme.Primary}]/status[/]", "Show authentication status");
        table.AddRow(
            $"[{Theme.Primary}]/storefront[/] [{Theme.Muted}]<code>[/]",
            "Set Apple Music storefront (default: us)"
        );
        table.AddRow(
            $"[{Theme.Primary}]/name[/] [{Theme.Muted}]<name>[/]",
            "Set playlist name for next transfer"
        );
        table.AddRow($"[{Theme.Primary}]/name[/]", "Clear playlist name override");
        table.AddRow(
            $"[{Theme.Primary}]/add[/] [{Theme.Muted}]<url>[/]",
            "Queue a Spotify playlist to merge multiple"
        );
        table.AddRow($"[{Theme.Primary}]/run[/]", "Start transferring the queued playlists");
        table.AddRow($"[{Theme.Primary}]/help[/]", "Show this help");
        table.AddRow($"[{Theme.Primary}]/quit[/]", "Exit");
        table.AddRow("", "");
        table.AddRow(
            $"[{Theme.Primary}]<spotify-url>[/]",
            "Paste a Spotify playlist URL to transfer directly"
        );

        return table;
    }

    internal static IRenderable RenderFooter() =>
        new Markup(
            $"[{Theme.Muted}]Commands: /help /auth /add /run /status /storefront <code> /name <name> /quit[/]"
        );

    internal static IRenderable RenderConfirmationFooter(TuiTransferPhase phase) =>
        phase switch
        {
            TuiTransferPhase.ConfirmTextMatch => new Markup(
                $"[{Theme.Primary}]Enter/Y[/] start matching  [{Theme.Muted}]N[/] skip  [{Theme.Muted}]Ctrl+C[/] quit"
            ),
            _ => new Markup(
                $"[{Theme.Primary}]Enter[/] start transfer  [{Theme.Muted}]Esc[/] back  [{Theme.Muted}]Ctrl+C[/] quit"
            ),
        };

    internal static IRenderable RenderScrollHint(int offset, int total, int visible)
    {
        var posInfo =
            total > visible
                ? $"  [{Theme.Muted}]({offset + 1}-{Math.Min(offset + visible, total)}/{total})[/]"
                : "";
        return new Markup(
            $"[{Theme.Muted}]\u2191/\u2193 scroll[/]{posInfo}[{Theme.Muted}]  \u2022  Press Enter to start a new transfer[/]"
        );
    }

    internal static IRenderable RenderPlaylistConfirmation(
        string name,
        int trackCount,
        string storefront
    )
    {
        var safeName = string.IsNullOrWhiteSpace(name) ? "Spotify Import" : name.Trim();
        var tracksLabel = trackCount == 1 ? "1 track" : $"{trackCount} tracks";
        var safeStorefront = string.IsNullOrWhiteSpace(storefront)
            ? "US"
            : storefront.ToUpperInvariant();
        const int nameMaxLength = 56;

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();

        grid.AddRow(
            new Markup($"[{Theme.Muted}]Name[/]"),
            new Markup($"[{Theme.White}]{Markup.Escape(Truncate(safeName, nameMaxLength))}[/]")
        );
        grid.AddRow(
            new Markup($"[{Theme.Muted}]Tracks[/]"),
            new Markup($"[{Theme.White} on {Theme.PrimaryDark}] {Markup.Escape(tracksLabel)} [/]")
        );
        grid.AddRow(
            new Markup($"[{Theme.Muted}]Storefront[/]"),
            new Markup($"[{Theme.White}]{Markup.Escape(safeStorefront)}[/]")
        );

        return new Panel(
            new Rows(new Markup($"[{Theme.White} bold]Playlist preview[/]"), new Text(""), grid)
        )
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Theme.GrayColor),
            Padding = new Padding(1, 0),
            Expand = false,
        };
    }

    internal static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max - 1), "\u2026");
}
