using Ciderfy.Matching;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Ciderfy.Tui;

internal static class BannerSection
{
    private const int CompactHeight = 1;
    private const int FullHeight = 6;
    private const int MinFullWidth = 72;

    private static readonly string[] _lines =
    [
        " ██████╗██╗██████╗ ███████╗██████╗ ███████╗██╗   ██╗",
        "██╔════╝██║██╔══██╗██╔════╝██╔══██╗██╔════╝╚██╗ ██╔╝",
        "██║     ██║██║  ██║█████╗  ██████╔╝█████╗   ╚████╔╝ ",
        "██║     ██║██║  ██║██╔══╝  ██╔══██╗██╔══╝    ╚██╔╝  ",
        "╚██████╗██║██████╔╝███████╗██║  ██║██║        ██║   ",
        " ╚═════╝╚═╝╚═════╝ ╚══════╝╚═╝  ╚═╝╚═╝        ╚═╝   ",
    ];

    private static readonly string[] _normalizedLines =
    [
        .. _lines.Select(static line => line.TrimEnd()),
    ];

    private static readonly int _width = _normalizedLines.Max(static line => line.Length);

    internal static int GetHeight(int width) =>
        ShouldUseCompact(width) ? CompactHeight : FullHeight;

    internal static IRenderable Render(int width)
    {
        if (ShouldUseCompact(width))
        {
            const string compactBanner = "CIDERFY";
            var compactPadding = Math.Max(0, (width - compactBanner.Length) / 2);
            return new Markup(
                $"[{Theme.Primary} bold]{new string(' ', compactPadding)}{compactBanner}[/]"
            );
        }

        var leftPadding = Math.Max(0, (width - _width) / 2);
        var rows = new List<IRenderable>(_normalizedLines.Length);

        for (var i = 0; i < _normalizedLines.Length; i++)
        {
            var bold = i is 2 or 3 ? " bold" : string.Empty;
            var centeredLine = string.Concat(new string(' ', leftPadding), _normalizedLines[i]);
            rows.Add(new Markup($"[{Theme.Primary}{bold}]{Markup.Escape(centeredLine)}[/]"));
        }

        return new Rows(rows);
    }

    private static bool ShouldUseCompact(int width) => width < Math.Max(_width, MinFullWidth);
}

internal static class CommandSuggestionSection
{
    private const int PanelChromeHeight = 2;

    internal static int GetHeight(int suggestionCount) =>
        suggestionCount == 0 ? 0 : suggestionCount + PanelChromeHeight;

    internal static int GetVisibleCount(int suggestionCount, int availableHeight) =>
        Math.Clamp(availableHeight - PanelChromeHeight, 0, suggestionCount);

    internal static IRenderable Render(
        IReadOnlyList<TuiCommandSuggestion> suggestions,
        int selectedIndex,
        int width
    )
    {
        var rows = new List<IRenderable>(suggestions.Count);

        for (var i = 0; i < suggestions.Count; i++)
        {
            rows.Add(RenderSuggestion(suggestions[i], i == selectedIndex));
        }

        return new Panel(new Rows(rows))
        {
            Header = new PanelHeader("Commands - arrows select, Enter/Tab accepts"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Theme.PrimaryColor),
            Padding = new Padding(1, 0),
            Width = width,
        };
    }

    private static Markup RenderSuggestion(TuiCommandSuggestion suggestion, bool selected)
    {
        var prefix = selected ? $"[{Theme.Primary}]→[/] " : "  ";
        var commandStyle = selected ? $"{Theme.Primary} bold" : Theme.Primary;
        return new Markup(
            $"{prefix}[{commandStyle}]{Markup.Escape(suggestion.Usage)}[/] [{Theme.Muted}]{Markup.Escape(suggestion.Description)}[/]"
        );
    }
}

internal static class FooterSection
{
    internal static Markup Render(
        TuiTransferPhase phase,
        bool awaitingUserToken,
        bool showHelp,
        bool showScrollActions
    )
    {
        if (showHelp)
        {
            return showScrollActions
                ? new Markup(
                    $"[{Theme.Primary}]Up/Down[/] scroll  [{Theme.Primary}]Enter[/] hide help  [{Theme.Primary}]Ctrl+C[/] quit"
                )
                : new Markup(
                    $"[{Theme.Primary}]Enter[/] hide help  [{Theme.Primary}]Ctrl+C[/] quit"
                );
        }

        if (awaitingUserToken)
        {
            return new Markup(
                $"[{Theme.Primary}]Enter[/] submit token  [{Theme.Muted}]Esc[/] cancel  [{Theme.Muted}]Ctrl+C[/] quit"
            );
        }

        if (phase is TuiTransferPhase.Done)
        {
            return showScrollActions
                ? new Markup(
                    $"[{Theme.Primary}]Up/Down[/] scroll  [{Theme.Primary}]Enter[/] new transfer  [{Theme.Muted}]Ctrl+C[/] quit"
                )
                : new Markup(
                    $"[{Theme.Primary}]Enter[/] new transfer  [{Theme.Muted}]Ctrl+C[/] quit"
                );
        }

        return phase switch
        {
            TuiTransferPhase.Idle => new Markup(
                $"[{Theme.Primary}]Enter[/] submit  [{Theme.Muted}]/help[/] commands  [{Theme.Muted}]Ctrl+C[/] quit"
            ),
            TuiTransferPhase.ConfirmPlaylist => new Markup(
                $"[{Theme.Primary}]Enter/Y[/] proceed  [{Theme.Muted}]Esc/N[/] cancel  [{Theme.Muted}]Ctrl+C[/] quit"
            ),
            TuiTransferPhase.ConfirmTextMatch => new Markup(
                $"[{Theme.Primary}]Enter/Y[/] match text  [{Theme.Muted}]N[/] skip  [{Theme.Muted}]Ctrl+C[/] quit"
            ),
            _ => new Markup($"[{Theme.Muted}]Working...[/]  [{Theme.Muted}]Ctrl+C[/] quit"),
        };
    }
}

internal static class HelpSection
{
    internal static int EntryCount => TuiCommands.HelpEntries.Length;

    internal static Table RenderTable(int scrollOffset, int visibleRows)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Theme.PrimaryColor)
            .AddColumn(new TableColumn("[bold]Command[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Description[/]"));

        var start = Math.Max(
            0,
            Math.Min(scrollOffset, Math.Max(0, TuiCommands.HelpEntries.Length - visibleRows))
        );
        var end = Math.Min(TuiCommands.HelpEntries.Length, start + visibleRows);

        for (var i = start; i < end; i++)
        {
            var entry = TuiCommands.HelpEntries[i];
            table.AddRow(RenderUsage(entry.Usage), entry.Description);
        }

        return table;
    }

    internal static Markup RenderScrollHint(int offset, int total, int visible)
    {
        if (total <= visible)
        {
            return new Markup(string.Empty);
        }

        return new Markup(
            $"[{Theme.Muted}]{Theme.ArrowUp}/{Theme.ArrowDown} scroll[/]{RenderHelpers.BuildScrollPositionInfo(offset, total, visible)}"
        );
    }

    private static string RenderUsage(string usage)
    {
        if (string.IsNullOrEmpty(usage))
        {
            return string.Empty;
        }

        var separatorIndex = usage.IndexOf(' ', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return $"[{Theme.Primary}]{Markup.Escape(usage)}[/]";
        }

        var command = usage[..separatorIndex];
        var arguments = usage[(separatorIndex + 1)..];
        return $"[{Theme.Primary}]{Markup.Escape(command)}[/] [{Theme.Muted}]{Markup.Escape(arguments)}[/]";
    }
}

internal static class InputSection
{
    internal static IRenderable Render(
        string buffer,
        bool showCursor,
        int width,
        string prompt = ">",
        string? placeholder = "Paste a Spotify playlist URL or run /help"
    )
    {
        var promptMarkup = $"[{Theme.Teal} bold]{Markup.Escape(prompt)} [/]";
        var text =
            string.IsNullOrEmpty(buffer) && placeholder is not null
                ? $"[{Theme.Muted}]{Markup.Escape(placeholder)}[/]"
                : $"[{Theme.White}]{Markup.Escape(buffer)}[/]";
        var cursor = showCursor ? $"[{Theme.Teal}]{Theme.CursorBlock}[/]" : string.Empty;

        return new Panel(new Markup(promptMarkup + text + cursor))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Theme.GrayColor),
            Padding = new Padding(1, 0),
            Width = width,
        };
    }
}

internal static class LogSection
{
    internal static Markup Render(LogBuffer logs, int width, int height)
    {
        var visible = logs.GetVisible(height);
        var lines = new List<string>(height);

        foreach (var entry in visible)
        {
            if (entry.Kind is LogKind.Separator)
            {
                var rule = new string(
                    Theme.SeparatorChar,
                    Math.Min(width, Theme.SeparatorMaxWidth)
                );
                lines.Add($"[{Theme.Gray}]{rule}[/]");
                continue;
            }

            var escaped = Markup.Escape(
                RenderHelpers.Truncate(entry.Message, width - Theme.LogPrefixWidth)
            );
            var (prefix, color) = entry.Kind switch
            {
                LogKind.Success => (Theme.LogPrefixSuccess, Theme.Teal),
                LogKind.Warning => (Theme.LogPrefixWarning, Theme.Cyan),
                LogKind.Error => (Theme.LogPrefixError, Theme.Red),
                _ => (Theme.LogPrefixInfo, Theme.Muted),
            };
            lines.Add($"[{color}]{prefix}{escaped}[/]");
        }

        if (lines.Count == 0)
        {
            lines.Add($"[{Theme.Muted}]No activity yet[/]");
        }

        while (lines.Count < height)
        {
            lines.Add(string.Empty);
        }

        return new Markup(string.Join('\n', lines));
    }
}

internal static class QueueSection
{
    internal const int MaxVisibleItems = 3;

    internal static Panel Render(IReadOnlyList<string> queuedIds)
    {
        var count = queuedIds.Count;
        var visibleIds = queuedIds.Take(MaxVisibleItems).ToList();
        var extra = count - visibleIds.Count;

        var rows = new List<IRenderable>(visibleIds.Count + 1);
        rows.AddRange(
            visibleIds.Select(id => new Markup(
                $"[{Theme.Primary}]{Theme.Bullet}[/] [{Theme.Muted}]spotify/playlist/[/][{Theme.White}]{Markup.Escape(id)}[/]"
            ))
        );

        if (extra > 0)
        {
            rows.Add(
                new Markup(
                    $"[{Theme.Primary}]{Theme.Bullet}[/] [{Theme.Primary}]+ {extra} more playlist(s)[/]"
                )
            );
        }

        return new Panel(new Rows(rows))
        {
            Header = new PanelHeader($"Queue ({count} ready to merge)"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Theme.PrimaryColor),
            Padding = new Padding(2, 0),
            Expand = false,
        };
    }
}

internal static class ResultsSection
{
    internal static Table RenderTable(
        List<MatchResult> allResults,
        int scrollOffset,
        int visibleRows,
        int width
    )
    {
        const int tableChromeWidth = 10;
        var indexWidth = Math.Max(
            2,
            allResults.Count.ToString(System.Globalization.CultureInfo.InvariantCulture).Length
        );
        var contentWidth = Math.Max(18, width - tableChromeWidth);
        var statusWidth = Math.Clamp(contentWidth / 3, 14, 26);
        var trackWidth = Math.Max(12, contentWidth - indexWidth - statusWidth);

        var table = new Table()
            .Border(TableBorder.Simple)
            .BorderColor(Theme.GrayColor)
            .AddColumn(new TableColumn("[bold]#[/]") { Width = indexWidth }.NoWrap())
            .AddColumn(new TableColumn("[bold]Track[/]") { Width = trackWidth }.NoWrap())
            .AddColumn(new TableColumn("[bold]Status[/]") { Width = statusWidth }.NoWrap());

        var start = Math.Max(0, scrollOffset);
        var end = Math.Min(allResults.Count, start + visibleRows);

        for (var i = start; i < end; i++)
        {
            var result = allResults[i];
            var trackLabel = BuildTrackLabel(result, trackWidth);

            switch (result)
            {
                case MatchResult.Matched m:
                    AddMatchedRow(table, i, trackLabel, m);
                    break;
                case MatchResult.NotFound nf:
                    AddNotFoundRow(table, i, trackLabel, nf, statusWidth);
                    break;
            }
        }

        return table;
    }

    internal static Panel RenderSummaryPanel(string name, int matched, int total, int notFound)
    {
        var success = matched > 0;
        var content =
            $"[{Theme.Primary}]{matched}[/][{Theme.Muted}]/{total} tracks transferred[/]"
            + (notFound > 0 ? $"  [{Theme.Red}]{notFound} not found[/]" : string.Empty);

        var header = success
            ? $" {Markup.Escape(RenderHelpers.Truncate(name, 48))} "
            : " Transfer failed ";
        var borderColor = success ? Theme.PrimaryColor : Theme.RedColor;

        return new Panel(new Markup(content))
        {
            Header = new PanelHeader(header),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor),
            Padding = new Padding(2, 0),
        };
    }

    internal static Markup RenderScrollHint(int offset, int total, int visible)
    {
        if (total <= visible)
        {
            return new Markup($"[{Theme.Muted}]Press Enter to start a new transfer[/]");
        }

        return new Markup(
            $"[{Theme.Muted}]{Theme.ArrowUp}/{Theme.ArrowDown} scroll[/]"
                + RenderHelpers.BuildScrollPositionInfo(offset, total, visible)
                + $"[{Theme.Muted}]  {Theme.Bullet}  Press Enter to start a new transfer[/]"
        );
    }

    private static string BuildTrackLabel(MatchResult result, int trackWidth) =>
        Markup.Escape(
            RenderHelpers.Truncate(
                $"{result.SpotifyTrack.Artist} - {result.SpotifyTrack.Title}",
                trackWidth
            )
        );

    private static void AddMatchedRow(
        Table table,
        int index,
        string trackLabel,
        MatchResult.Matched match
    )
    {
        var detail = RenderMatchedStatus(match);
        table.AddRow($"[{Theme.Gray}]{index + 1}[/]", trackLabel, Theme.LogPrefixSuccess + detail);
    }

    private static void AddNotFoundRow(
        Table table,
        int index,
        string trackLabel,
        MatchResult.NotFound notFound,
        int statusWidth
    )
    {
        var reason = NormalizeNotFoundReason(notFound.Reason);
        table.AddRow(
            $"[{Theme.Muted}]{index + 1}[/]",
            $"[{Theme.Red}]{trackLabel}[/]",
            $"{Theme.LogPrefixError}[{Theme.Red}]{Markup.Escape(RenderHelpers.Truncate(reason, statusWidth))}[/]"
        );
    }

    private static string RenderMatchedStatus(MatchResult.Matched match) =>
        match.Method is MatchMethod.Isrc
            ? $"[{Theme.Primary}]ISRC[/]"
            : $"[{Theme.Teal}]Text ({(int)(match.Confidence * 100)}%)[/]";

    private static string NormalizeNotFoundReason(string reason) =>
        reason.StartsWith(
            MatchResult.NotFound.BelowThresholdPrefix,
            StringComparison.OrdinalIgnoreCase
        )
            ? $"Below threshold{reason[MatchResult.NotFound.BelowThresholdPrefix.Length..]}"
            : reason;
}

internal static class StatusSection
{
    private static readonly string[] _stepLabels = ["Setup", "Fetch", "Match", "Transfer", "Done"];

    internal static Align RenderBadges(
        bool hasValidDeveloperToken,
        bool hasValidUserToken,
        string storefront,
        string? nextPlaylistName
    )
    {
        var devBadge = hasValidDeveloperToken
            ? RenderHelpers.Badge("Developer valid", Theme.BadgeFg, Theme.BadgeGoodBg)
            : RenderHelpers.Badge("Developer missing", Theme.BadgeFg, Theme.BadgeBadBg);

        var userBadge = hasValidUserToken
            ? RenderHelpers.Badge("User valid", Theme.BadgeFg, Theme.BadgeGoodBg)
            : RenderHelpers.Badge("User missing", Theme.BadgeFg, Theme.BadgeBadBg);

        var sfBadge = RenderHelpers.Badge(
            $"Storefront {storefront.ToUpperInvariant()}",
            Theme.BadgeFg,
            Theme.BadgeNeutralBg
        );
        var nameBadge = string.IsNullOrWhiteSpace(nextPlaylistName)
            ? RenderHelpers.Badge("Name auto", Theme.BadgeFg, Theme.BadgeNeutralBg)
            : RenderHelpers.Badge("Name custom", Theme.BadgeFg, Theme.BadgeNeutralBg);

        return Align.Center(new Markup($"{devBadge} {userBadge} {sfBadge} {nameBadge}"));
    }

    internal static IRenderable RenderStepper(TuiTransferPhase phase)
    {
        var currentStep = phase switch
        {
            TuiTransferPhase.FetchingPlaylist => 1,
            TuiTransferPhase.ResolvingIsrc
            or TuiTransferPhase.ConfirmTextMatch
            or TuiTransferPhase.TextMatching => 2,
            TuiTransferPhase.CreatingPlaylist => 3,
            TuiTransferPhase.Done => 4,
            _ => 0,
        };

        var parts = _stepLabels.Select(
            (step, index) =>
            {
                var escapedStep = Markup.Escape(step);
                if (index < currentStep)
                {
                    return $"[{Theme.Teal}]{escapedStep}[/]";
                }

                if (index == currentStep)
                {
                    return $"[{Theme.Primary} bold]{escapedStep}[/]";
                }

                return $"[{Theme.Muted}]{escapedStep}[/]";
            }
        );

        return Align.Center(
            new Markup(string.Join($" [{Theme.Gray}]{Theme.ChevronRight}[/] ", parts))
        );
    }
}

internal static class TransferProgressSection
{
    internal static Markup RenderProgressBar(string label, double percent, int width)
    {
        var barWidth = Math.Max(10, width - 10);
        var filled = (int)(barWidth * Math.Clamp(percent, 0, 1));
        var empty = barWidth - filled;

        var bar = new string(Theme.ProgressFilled, filled) + new string(Theme.ProgressEmpty, empty);
        var pctText = $" {(int)(percent * 100)}%";

        return new Markup(
            $"[{Theme.Teal}]{Markup.Escape(label)}[/]\n"
                + $"[{Theme.Primary}]{bar}[/]"
                + $"[{Theme.Muted}]{pctText}[/]"
        );
    }

    internal static Markup RenderSpinnerLine(string label, int spinnerTick)
    {
        var frame = Theme.SpinnerFrames[spinnerTick % Theme.SpinnerFrames.Length];
        return new Markup($"[{Theme.Primary}]{frame}[/] [{Theme.Teal}]{Markup.Escape(label)}[/]");
    }

    internal static Markup RenderConfirmPrompt(int count) =>
        new(
            $"[{Theme.White} bold]"
                + $"Try text matching for {count} remaining track(s)? (can be slow) [[Y/n]] [/]"
        );

    internal static Panel RenderPlaylistConfirmation(string name, int trackCount, string storefront)
    {
        var safeName = string.IsNullOrWhiteSpace(name) ? "Spotify Import" : name.Trim();
        var tracksLabel = trackCount == 1 ? "1 track" : $"{trackCount} tracks";
        var safeStorefront = string.IsNullOrWhiteSpace(storefront)
            ? "US"
            : storefront.ToUpperInvariant();
        const int nameMaxLength = 56;

        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap())
            .AddColumn()
            .AddRow(
                new Markup($"[{Theme.Muted}]Name[/]"),
                new Markup(
                    $"[{Theme.White}]{Markup.Escape(RenderHelpers.Truncate(safeName, nameMaxLength))}[/]"
                )
            )
            .AddRow(
                new Markup($"[{Theme.Muted}]Tracks[/]"),
                new Markup(RenderHelpers.Badge(tracksLabel, Theme.BadgeFg, Theme.BadgeGoodBg))
            )
            .AddRow(
                new Markup($"[{Theme.Muted}]Storefront[/]"),
                new Markup($"[{Theme.White}]{Markup.Escape(safeStorefront)}[/]")
            );

        return new Panel(
            new Rows(
                new Markup($"[{Theme.White} bold]Playlist preview[/]"),
                new Text(string.Empty),
                grid
            )
        )
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Theme.GrayColor),
            Padding = new Padding(1, 0),
            Expand = false,
        };
    }
}
