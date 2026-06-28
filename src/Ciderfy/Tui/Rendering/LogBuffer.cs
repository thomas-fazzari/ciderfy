namespace Ciderfy.Tui;

/// <summary>
/// Severity level for a log entry displayed in the TUI activity area
/// </summary>
internal enum LogKind
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3,
    Separator = 4,
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
    private readonly Queue<LogEntry> _entries = new(capacity);

    /// <summary>
    /// Appends a new log entry by overwriting the oldest one when capacity is reached
    /// </summary>
    internal void Append(LogKind kind, string message)
    {
        if (_entries.Count == capacity)
        {
            _entries.Dequeue();
        }

        _entries.Enqueue(new LogEntry(kind, message.Trim()));
    }

    internal void Clear()
    {
        _entries.Clear();
    }

    /// <summary>
    /// Returns the most recent entries that fit within the given height
    /// </summary>
    internal ReadOnlySpan<LogEntry> GetVisible(int height)
    {
        var take = Math.Min(height, _entries.Count);
        if (take == 0)
        {
            return [];
        }

        return _entries.Skip(_entries.Count - take).ToArray();
    }
}
