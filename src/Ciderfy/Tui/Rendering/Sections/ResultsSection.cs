using Ciderfy.Matching;
using Spectre.Console;

namespace Ciderfy.Tui;

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
