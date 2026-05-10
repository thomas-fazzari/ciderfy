using Spectre.Console;

namespace Ciderfy.Tui;

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
