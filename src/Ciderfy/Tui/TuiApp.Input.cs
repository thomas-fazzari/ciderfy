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

            if (TryHandlePlaylistConfirmInput(key))
                continue;

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

    private bool TryHandlePlaylistConfirmInput(ConsoleKeyInfo key)
    {
        if (_phase is not TuiTransferPhase.ConfirmPlaylist)
            return false;

        if (key.Key is ConsoleKey.Enter)
        {
            _phase = TuiTransferPhase.ResolvingIsrc;
            _progressCurrent = 0;
            _progressTotal = _transferTracks?.Count ?? 0;
            _logs.Append(LogKind.Info, "Starting ISRC matching...");
            _ = Task.Run(() => RunIsrcMatchAsync(_cts.Token));
        }
        else if (key.Key is ConsoleKey.Escape or ConsoleKey.Backspace)
        {
            ResetTransferState();
            _logs.Append(LogKind.Info, "Transfer cancelled.");
        }

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
            Console.WindowHeight - CurrentFixedChromeHeight - DoneViewChromeHeight
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
            if (_queuedPlaylistUrls.Count > 0)
            {
                _queuedPlaylistUrls.Add(urlInfo.Id);
                _logs.Append(
                    LogKind.Success,
                    $"Added to merge queue (Total: {_queuedPlaylistUrls.Count}). Type /run to start."
                );
                return;
            }

            ResetTransferState();
            _phase = TuiTransferPhase.FetchingPlaylist;
            _logs.Append(LogKind.Info, "Starting transfer...");
            _ = Task.Run(() => RunFetchPlaylistAsync([urlInfo.Id], _cts.Token));
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
        _commands.Register(HandleAddCommand, "/add");
        _commands.Register(HandleRunCommand, "/run");

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

    private void HandleAddCommand(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            _logs.Append(LogKind.Error, "Usage: /add <url1> [url2] ...");
            return;
        }

        var urls = argument.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        var addedCount = 0;

        foreach (var url in urls)
        {
            if (SpotifyUrlInfo.TryParse(url, out var urlInfo) && urlInfo is not null)
            {
                if (!_queuedPlaylistUrls.Contains(urlInfo.Id))
                {
                    _queuedPlaylistUrls.Add(urlInfo.Id);
                    addedCount++;
                }
            }
        }

        if (addedCount > 0)
        {
            _logs.Append(
                LogKind.Success,
                $"Added {addedCount} playlists to queue. ({_queuedPlaylistUrls.Count} total)"
            );
            _logs.Append(LogKind.Info, "Type /run to start merging.");
        }
        else
        {
            _logs.Append(LogKind.Error, "No valid Spotify URLs found.");
        }
    }

    private void HandleRunCommand(string? argument)
    {
        if (_queuedPlaylistUrls.Count == 0)
        {
            _logs.Append(LogKind.Error, "Queue is empty. Add playlists with /add <url>");
            return;
        }

        var playlistIdsToFetch = _queuedPlaylistUrls.ToList();
        _queuedPlaylistUrls.Clear();

        ResetTransferState();
        _phase = TuiTransferPhase.FetchingPlaylist;
        _logs.Append(
            LogKind.Info,
            $"Starting transfer of {playlistIdsToFetch.Count} merged playlists..."
        );
        _ = Task.Run(() => RunFetchPlaylistAsync(playlistIdsToFetch, _cts.Token));
    }
}
