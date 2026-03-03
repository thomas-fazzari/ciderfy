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
    private readonly LogEntry[] _entries = new LogEntry[capacity];
    private int _head; // index of the oldest entry
    private int _count; // number of valid entries

    /// <summary>
    /// Appends a new log entry by overwriting the oldest one when capacity is reached
    /// </summary>
    internal void Append(LogKind kind, string message)
    {
        var index = (_head + _count) % capacity;
        _entries[index] = new LogEntry(kind, message.Trim());

        if (_count < capacity)
            _count++;
        else
            _head = (_head + 1) % capacity;
    }

    /// <summary>
    /// Returns the most recent entries that fit within the given height
    /// </summary>
    internal ReadOnlySpan<LogEntry> GetVisible(int height)
    {
        var take = Math.Min(height, _count);
        if (take == 0)
            return [];

        var buffer = new LogEntry[take];
        var startOffset = _count - take; // how many entries to skip from the oldest

        for (var i = 0; i < take; i++)
            buffer[i] = _entries[(_head + startOffset + i) % capacity];

        return buffer;
    }

    internal bool IsEmpty => _count == 0;
}
