using Spectre.Console;

namespace Ciderfy.Tui;

internal static class RenderHelpers
{
    internal static string Badge(string text, string fg, string bg) =>
        $"[{fg} on {bg}] {Markup.Escape(text)} [/]";

    internal static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max - 1), Theme.Ellipsis);

    internal static string BuildScrollPositionInfo(int offset, int total, int visible) =>
        $"  [{Theme.Muted}]({offset + 1}-{Math.Min(offset + visible, total)}/{total})[/]";
}
