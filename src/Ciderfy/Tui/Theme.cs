using Spectre.Console;

namespace Ciderfy.Tui;

/// <summary>
/// Color palette and style constants for the TUI
/// </summary>
internal static class Theme
{
    internal const string Primary = "#7aa2f7";
    internal const string Gray = "#414868";
    internal const string White = "#c0caf5";
    internal const string Muted = "#a9b1d6";
    internal const string Red = "#f7768e";
    internal const string Cyan = "#89ddff";
    internal const string Teal = "#7dcfff";

    internal const string BadgeFg = "#15161e";
    internal const string BadgeGoodBg = "#bb9af7";
    internal const string BadgeBadBg = "#f7768e";
    internal const string BadgeNeutralBg = "#7aa2f7";

    internal const string LogPrefixSuccess = "\u2714 ";
    internal const string LogPrefixWarning = "\u26a0 ";
    internal const string LogPrefixError = "\u2718 ";
    internal static readonly string LogPrefixInfo = new(' ', LogPrefixSuccess.Length);
    internal static readonly int LogPrefixWidth = LogPrefixSuccess.Length;

    internal static readonly Color GrayColor = Color.FromHex(Gray);
    internal static readonly Color PrimaryColor = Color.FromHex(Primary);
    internal static readonly Color RedColor = Color.FromHex(Red);

    internal const char SeparatorChar = '\u2500';
    internal const int SeparatorMaxWidth = 40;

    internal const char ProgressFilled = '\u2588';
    internal const char ProgressEmpty = '\u2591';
    internal const string CursorBlock = "\u2588";
    internal const string Ellipsis = "\u2026";
    internal const string ArrowUp = "\u2191";
    internal const string ArrowDown = "\u2193";
    internal const string ChevronRight = "\u203A";
    internal const string Bullet = "\u2022";

    internal static readonly string[] SpinnerFrames =
    [
        "\u280b",
        "\u2819",
        "\u2839",
        "\u2838",
        "\u283c",
        "\u2834",
        "\u2826",
        "\u2827",
        "\u2807",
        "\u280f",
    ];
}
