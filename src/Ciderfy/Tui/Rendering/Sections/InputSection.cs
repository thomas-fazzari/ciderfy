using Spectre.Console;
using Spectre.Console.Rendering;

namespace Ciderfy.Tui;

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
