using Spectre.Console;

namespace Ciderfy.Tui;

/// <summary>
/// Color palette and style constants for the TUI
/// </summary>
internal static class Theme
{
    internal const string Primary = "#89B4FA";
    internal const string PrimaryDark = "#89B4FA";
    internal const string Gray = "#313244";
    internal const string Black = "#11111B";
    internal const string BlackAlt = "#181825";
    internal const string White = "#CDD6F4";
    internal const string Muted = "#A6ADC8";
    internal const string Green = "#A6E3A1";
    internal const string Red = "#F38BA8";
    internal const string Yellow = "#F9E2AF";
    internal const string Teal = "#94E2D5";
    internal const string Accent = "#313244";

    internal const string BadgeGoodBg = "#89B4FA";
    internal const string BadgeGoodFg = "#11111B";
    internal const string BadgeBadBg = "#89B4FA";
    internal const string BadgeBadFg = "#11111B";
    internal const string BadgeNeutralBg = "#89B4FA";
    internal const string BadgeNeutralFg = "#11111B";

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
