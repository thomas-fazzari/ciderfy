using Ciderfy.Matching;
using Spectre.Console;

namespace Ciderfy;

internal static class TuiHelper
{
    internal static readonly Color AccentColor = new(29, 185, 84);
    internal const string Accent = "#1db954";

    internal static void PrintBanner()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            "[#40f080]     ██████╗██╗██████╗ ███████╗██████╗ ███████╗██╗   ██╗[/]"
        );
        AnsiConsole.MarkupLine(
            "[#34dc6c]    ██╔════╝██║██╔══██╗██╔════╝██╔══██╗██╔════╝╚██╗ ██╔╝[/]"
        );
        AnsiConsole.MarkupLine(
            "[bold #1db954]    ██║     ██║██║  ██║█████╗  ██████╔╝█████╗   ╚████╔╝[/]"
        );
        AnsiConsole.MarkupLine(
            "[bold #1db954]    ██║     ██║██║  ██║██╔══╝  ██╔══██╗██╔══╝    ╚██╔╝[/]"
        );
        AnsiConsole.MarkupLine("[#128a3c]    ╚██████╗██║██████╔╝███████╗██║  ██║██║        ██║[/]");
        AnsiConsole.MarkupLine("[#0d7030]     ╚═════╝╚═╝╚═════╝ ╚══════╝╚═╝  ╚═╝╚═╝        ╚═╝[/]");
        AnsiConsole.WriteLine();
    }

    internal static void PrintHelp()
    {
        var table = new Table().Border(TableBorder.Rounded).BorderColor(AccentColor);
        table.AddColumn(new TableColumn($"[bold]Command[/]").NoWrap());
        table.AddColumn("[bold]Description[/]");

        table.AddRow($"[{Accent}]/auth[/]", "Authenticate with Apple Music");
        table.AddRow($"[{Accent}]/auth reset[/]", "Clear cached tokens and re-authenticate");
        table.AddRow($"[{Accent}]/status[/]", "Show authentication status");
        table.AddRow(
            $"[{Accent}]/storefront[/] [grey]<code>[/]",
            "Set Apple Music storefront (default: us)"
        );
        table.AddRow($"[{Accent}]/name[/] [grey]<name>[/]", "Set playlist name for next transfer");
        table.AddRow($"[{Accent}]/name[/]", "Clear playlist name override");
        table.AddRow($"[{Accent}]/help[/]", "Show this help");
        table.AddRow($"[{Accent}]/quit[/]", "Exit");
        table.AddRow("", "");
        table.AddRow(
            $"[{Accent}]<spotify-playlist-url>[/]",
            "Paste a Spotify playlist URL to transfer"
        );

        AnsiConsole.Write(table);
    }

    internal static void PrintTransferResult(
        string? applePlaylistId,
        bool success,
        string name,
        int matched,
        int total,
        int notFound
    )
    {
        if (applePlaylistId is null)
        {
            AnsiConsole.MarkupLine("[red]Failed to create playlist.[/]");
            return;
        }

        var panel = new Panel(
            $"[{Accent}]{matched}[/]/{total} tracks transferred"
                + (notFound > 0 ? $"  [red]{notFound} not found[/]" : "")
        )
        {
            Header = new PanelHeader(success ? $" {Markup.Escape(name)} " : " Transfer failed "),
            Border = BoxBorder.Rounded,
            BorderStyle = success ? new Style(AccentColor) : new Style(Color.Red),
        };

        AnsiConsole.Write(panel);
    }

    internal static void PrintMatchResults(List<MatchResult> results)
    {
        var table = new Table().Border(TableBorder.Simple).BorderColor(Color.Grey);
        table.AddColumn("#");
        table.AddColumn("Track");
        table.AddColumn("Status");

        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var trackLabel = Markup.Escape(
                Truncate($"{r.SpotifyTrack.Artist} - {r.SpotifyTrack.Title}", 55)
            );

            switch (r)
            {
                case MatchResult.Matched m:
                    var detail = m.Method == "ISRC" ? "ISRC" : $"text {m.Confidence:F2}";
                    table.AddRow($"{i + 1}", trackLabel, $"[{Accent}]{Markup.Escape(detail)}[/]");
                    break;
                case MatchResult.NotFound nf:
                    table.AddRow($"{i + 1}", trackLabel, $"[red]{Markup.Escape(nf.Reason)}[/]");
                    break;
            }
        }

        AnsiConsole.Write(table);
    }

    internal static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max - 1), "\u2026");

    internal static async Task<T> WithStatusAsync<T>(string message, Func<Task<T>> action)
    {
        T result = default!;
        await AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(new Style(AccentColor))
            .StartAsync(message, async _ => result = await action());
        return result;
    }

    internal static Task WithProgressAsync(string description, Func<ProgressTask, Task> action) =>
        AnsiConsole
            .Progress()
            .AutoClear(true)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn { CompletedStyle = new Style(AccentColor) },
                new PercentageColumn(),
                new SpinnerColumn { Style = new Style(AccentColor) }
            )
            .StartAsync(async ctx => await action(ctx.AddTask(description)));
}
