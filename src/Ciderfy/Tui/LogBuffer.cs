using System.Runtime.InteropServices;

namespace Ciderfy.Tui;

/// <summary>
/// Severity level for a log entry displayed in the TUI activity area
/// </summary>
internal enum LogKind
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>
/// Single log line with a severity kind and message
/// </summary>
internal readonly record struct LogEntry(LogKind Kind, string Message);

/// <summary>
/// Fixed-capacity ring buffer that stores the most recent log entries for TUI display
/// </summary>
internal sealed class LogBuffer(int capacity = 500)
{
    private readonly List<LogEntry> _entries = new(64);

    /// <summary>
    /// Appends a new log entry, evicting the oldest entries when capacity is exceeded
    /// </summary>
    internal void Append(LogKind kind, string message)
    {
        _entries.Add(new LogEntry(kind, message.Trim()));
        if (_entries.Count > capacity)
            _entries.RemoveRange(0, _entries.Count - capacity);
    }

    /// <summary>
    /// Returns the most recent entries that fit within the given height
    /// </summary>
    internal ReadOnlySpan<LogEntry> GetVisible(int height) =>
        _entries.Count <= height
            ? CollectionsMarshal.AsSpan(_entries)
            : CollectionsMarshal.AsSpan(_entries)[(_entries.Count - height)..];

    internal bool IsEmpty => _entries.Count == 0;
}
