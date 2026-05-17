namespace Ciderfy.Tui;

/// <summary>
/// Parsed command action understood by the controller
/// </summary>
internal enum TuiCommandKind
{
    Unknown = 0,
    Add = 1,
    Auth = 2,
    AuthReset = 3,
    Config = 4,
    Help = 5,
    Name = 6,
    Quit = 7,
    Run = 8,
    Status = 9,
    Storefront = 10,
}

/// <summary>
/// Command row shown in help
/// </summary>
internal sealed record TuiCommandHelpEntry(string Usage, string Description);

/// <summary>
/// Command completion shown while typing commands
/// </summary>
internal sealed record TuiCommandSuggestion(string Completion, string Usage, string Description);

/// <summary>
/// Parsed user command, including the original command name and optional argument
/// </summary>
internal sealed record TuiCommand(TuiCommandKind Kind, string Name, string? Argument);
