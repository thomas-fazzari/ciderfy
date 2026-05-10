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
