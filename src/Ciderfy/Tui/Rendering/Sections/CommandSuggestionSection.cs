using Spectre.Console;
using Spectre.Console.Rendering;

namespace Ciderfy.Tui;

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
            var suggestion = suggestions[i];
            var selected = i == selectedIndex;
            rows.Add(RenderSuggestion(suggestion, selected));
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
