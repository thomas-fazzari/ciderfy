using Spectre.Console;

namespace Ciderfy.Tui;

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
            lines.Add($"[{Theme.Muted}]No activity yet[/]");

        while (lines.Count < height)
            lines.Add(string.Empty);

        return new Markup(string.Join('\n', lines));
    }
}
