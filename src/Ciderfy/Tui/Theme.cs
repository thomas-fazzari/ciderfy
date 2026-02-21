using Spectre.Console;

namespace Ciderfy.Tui;

/// <summary>
/// Color palette and style constants for the TUI
/// </summary>
internal static class Theme
{
    internal const string Primary = "#1ED760";
    internal const string PrimaryDark = "#129D48";
    internal const string Gray = "#2C2C2C";
    internal const string Black = "#0B0B0B";
    internal const string BlackAlt = "#151515";
    internal const string White = "#F2F2F2";
    internal const string Muted = "#BCBCBC";
    internal const string Green = "#7CDD83";
    internal const string Red = "#F87171";
    internal const string Yellow = "#F2C14E";
    internal const string Teal = "#C6C6C6";
    internal const string Accent = "#57E08D";

    internal const string BadgeGoodBg = "#3AA457";
    internal const string BadgeGoodFg = "#EEFFF4";
    internal const string BadgeBadBg = "#4A231F";
    internal const string BadgeBadFg = "#FFB4A8";
    internal const string BadgeNeutralBg = "#232323";
    internal const string BadgeNeutralFg = "#E4E4E4";

    internal static readonly string[] BannerColors =
    [
        "#A9F0BE",
        "#85EAA4",
        "#61E289",
        "#3FDB6E",
        "#22C95A",
        "#129D48",
    ];

    // Color instances for Spectre API, when it requires Color
    internal static readonly Color GrayColor = Color.FromHex(Gray);
    internal static readonly Color PrimaryColor = Color.FromHex(Primary);
    internal static readonly Color PrimaryDarkColor = Color.FromHex(PrimaryDark);
    internal static readonly Color RedColor = Color.FromHex(Red);

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
