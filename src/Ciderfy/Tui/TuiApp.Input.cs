using Ciderfy.Matching;
using Ciderfy.Spotify;

namespace Ciderfy.Tui;

internal sealed partial class TuiApp
{
    private void ReadKeysLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!TryReadKey(out var key))
                continue;

            if (HandleQuitShortcut(key))
                return;

            if (TryHandleConfirmPhaseInput(key))
                continue;

            if (TryHandleDonePhaseInput(key))
                continue;

            if (!CanAcceptGeneralInput())
                continue;

            HandleGeneralInput(key);
        }
    }

    private static bool TryReadKey(out ConsoleKeyInfo key)
    {
        if (!Console.KeyAvailable)
        {
            Thread.Sleep(20);
            key = default;
            return false;
        }

        key = Console.ReadKey(true);
        return true;
    }

    private bool HandleQuitShortcut(ConsoleKeyInfo key)
    {
        if (key is not { Key: ConsoleKey.C, Modifiers: ConsoleModifiers.Control })
            return false;

        _quit = true;
        _cts.Cancel();
        return true;
    }

    private bool TryHandleConfirmPhaseInput(ConsoleKeyInfo key)
    {
        if (_phase is not TuiTransferPhase.ConfirmTextMatch)
            return false;

        HandleConfirmKey(key);
        return true;
    }

    private bool TryHandleDonePhaseInput(ConsoleKeyInfo key)
    {
        if (_phase is not TuiTransferPhase.Done)
            return false;

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                ScrollResultsUp();
                return true;
            case ConsoleKey.DownArrow:
                ScrollResultsDown();
                return true;
            case ConsoleKey.Enter:
                ResetTransferState();
                return true;
            default:
                return false;
        }
    }

    private void ScrollResultsUp() => _scrollOffset = Math.Max(0, _scrollOffset - 1);

    private void ScrollResultsDown()
    {
        if (_allResults is null)
            return;

        var visibleRows = Math.Max(
            3,
            Console.WindowHeight - FixedChromeHeight - DoneViewChromeHeight
        );
        _scrollOffset = Math.Min(Math.Max(0, _allResults.Count - visibleRows), _scrollOffset + 1);
    }

    private bool CanAcceptGeneralInput() =>
        _phase is TuiTransferPhase.Idle or TuiTransferPhase.Done;

    private void HandleGeneralInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                HandleEnter();
                break;
            case ConsoleKey.Backspace:
                RemoveLastInputCharacter();
                break;
            case ConsoleKey.Escape:
                _inputBuffer.Clear();
                break;
            default:
                AppendInputCharacter(key.KeyChar);
                break;
        }
    }

    private void RemoveLastInputCharacter()
    {
        if (_inputBuffer.Length > 0)
            _inputBuffer.Remove(_inputBuffer.Length - 1, 1);
    }

    private void AppendInputCharacter(char keyChar)
    {
        if (keyChar >= 32)
            _inputBuffer.Append(keyChar);
    }

    private void HandleConfirmKey(ConsoleKeyInfo key)
    {
        switch (char.ToLowerInvariant(key.KeyChar))
        {
            case 'y' or '\r' or '\n':
                _phase = TuiTransferPhase.TextMatching;
                _progressCurrent = 0;
                _progressTotal = _unmatchedTracks?.Count ?? 0;
                _progressLabel = "";
                _logs.Append(LogKind.Info, "Starting text matching...");
                _ = Task.Run(() => RunTextMatchAsync(_cts.Token));
                break;
            case 'n':
                _logs.Append(LogKind.Info, "Text matching skipped.");
                if (_unmatchedTracks is not null)
                {
                    _textResults ??= [];
                    foreach (var t in _unmatchedTracks)
                        _textResults.Add(new MatchResult.NotFound(t, "Skipped"));
                }

                _phase = TuiTransferPhase.CreatingPlaylist;
                _ = Task.Run(() => RunCreatePlaylistAsync(_cts.Token));
                break;
        }
    }

    private void HandleEnter()
    {
        if (_phase is TuiTransferPhase.Done)
        {
            ResetTransferState();
            return;
        }

        _showHelp = false;

        var raw = _inputBuffer.ToString().Trim();
        _inputBuffer.Clear();

        if (string.IsNullOrEmpty(raw))
            return;

        if (_awaitingUserToken)
        {
            HandleUserTokenInput(raw);
            return;
        }

        if (raw.StartsWith('/'))
        {
            HandleCommand(raw);
            return;
        }

        if (SpotifyUrlInfo.TryParse(raw, out var urlInfo) && urlInfo is not null)
        {
            ResetTransferState();
            _phase = TuiTransferPhase.FetchingPlaylist;
            _logs.Append(LogKind.Info, "Starting transfer...");
            _ = Task.Run(() => RunFetchPlaylistAsync(urlInfo.Id, _cts.Token));
            return;
        }

        _logs.Append(LogKind.Error, "Not a valid command or Spotify URL. Type /help");
    }

    private void HandleUserTokenInput(string raw)
    {
        var trimmed = raw.Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            _logs.Append(LogKind.Error, "No token provided.");
            return;
        }

        tokenCache.UserToken = trimmed;
        tokenCache.UserTokenExpiry = DateTimeOffset.UtcNow.AddMonths(3);
        tokenCache.Save();

        _awaitingUserToken = false;
        _logs.Append(LogKind.Success, "Authentication complete! Tokens cached.");
        _logs.Append(LogKind.Info, StatusSummary());
    }

    private void HandleCommand(string raw)
    {
        var parts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0];
        var arg = parts.Length > 1 ? parts[1].Trim() : null;

        EnsureCommandsRegistered();

        if (_commands.TryExecute(cmd, arg))
            return;

        _logs.Append(LogKind.Error, $"Unknown command: {cmd.ToLowerInvariant()}. Type /help");
    }

    private void EnsureCommandsRegistered()
    {
        if (_commandsRegistered)
            return;

        _commands.Register(HandleQuitCommand, "/quit", "/exit", "/q");
        _commands.Register(HandleHelpCommand, "/help", "/h");
        _commands.Register(HandleStatusCommand, "/status");
        _commands.Register(HandleStorefrontCommand, "/storefront", "/sf");
        _commands.Register(HandleNameCommand, "/name");
        _commands.Register(HandleAuthCommand, "/auth");

        _commandsRegistered = true;
    }

    private void HandleQuitCommand(string? _)
    {
        _quit = true;
        _cts.Cancel();
    }

    private void HandleHelpCommand(string? _) => _showHelp = !_showHelp;

    private void HandleStatusCommand(string? _) => _logs.Append(LogKind.Info, StatusSummary());

    private void HandleStorefrontCommand(string? argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            _logs.Append(LogKind.Info, $"Storefront: {_storefront}. Usage: /storefront fr");
            return;
        }

        _storefront = argument.ToLowerInvariant();
        _logs.Append(LogKind.Success, $"Storefront set to {_storefront}");
    }

    private void HandleNameCommand(string? argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            _nextPlaylistName = null;
            _logs.Append(LogKind.Success, "Playlist name override cleared");
            return;
        }

        _nextPlaylistName = argument;
        _logs.Append(LogKind.Success, $"Next playlist name set to \"{_nextPlaylistName}\"");
    }

    private void HandleAuthCommand(string? argument)
    {
        if ("reset".Equals(argument, StringComparison.OrdinalIgnoreCase))
        {
            tokenCache.Clear();
            _logs.Append(LogKind.Warning, "Tokens cleared.");
        }

        _logs.Append(LogKind.Info, "Authenticating...");
        _ = Task.Run(() => RunAuthAsync(_cts.Token));
    }
}
