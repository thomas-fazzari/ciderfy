using Spectre.Console;

namespace Ciderfy.Tui;

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
