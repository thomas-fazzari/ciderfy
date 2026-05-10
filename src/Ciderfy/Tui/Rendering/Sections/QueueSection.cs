using Spectre.Console;
using Spectre.Console.Rendering;

namespace Ciderfy.Tui;

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
