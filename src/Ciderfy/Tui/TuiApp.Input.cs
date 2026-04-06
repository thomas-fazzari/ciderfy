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

        _state.QuitRequested = true;
        _cts.Cancel();
        return true;
    }

    private bool TryHandlePlaylistConfirmInput(ConsoleKeyInfo key)
    {
        if (_state.Phase is not TuiTransferPhase.ConfirmPlaylist)
            return false;

        if (key.Key is ConsoleKey.Enter)
        {
            _state.Phase = TuiTransferPhase.ResolvingIsrc;
            _state.ProgressCurrent = 0;
            _state.ProgressTotal = _state.TransferTracks?.Count ?? 0;
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
        if (_state.Phase is not TuiTransferPhase.ConfirmTextMatch)
            return false;

        HandleConfirmKey(key);
        return true;
    }

    private bool TryHandleDonePhaseInput(ConsoleKeyInfo key)
    {
        if (_state.Phase is not TuiTransferPhase.Done)
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

    private void ScrollResultsUp() => _state.ScrollOffset = Math.Max(0, _state.ScrollOffset - 1);

    private void ScrollResultsDown()
    {
        if (_state.AllResults is null)
            return;

        var visibleRows = Math.Max(
            3,
            Console.WindowHeight - CurrentFixedChromeHeight - DoneViewChromeHeight
        );
        _state.ScrollOffset = Math.Min(
            Math.Max(0, _state.AllResults.Count - visibleRows),
            _state.ScrollOffset + 1
        );
    }

    private bool CanAcceptGeneralInput() => _state.Phase is TuiTransferPhase.Idle;

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
                _state.Phase = TuiTransferPhase.TextMatching;
                _state.ProgressCurrent = 0;
                _state.ProgressTotal = _state.UnmatchedTracks?.Count ?? 0;
                _state.ProgressLabel = string.Empty;
                _logs.Append(LogKind.Info, "Starting text matching...");
                _ = Task.Run(() => RunTextMatchAsync(_cts.Token));
                break;
            case 'n':
                _logs.Append(LogKind.Info, "Text matching skipped.");
                if (_state.UnmatchedTracks is not null)
                {
                    _state.TextResults ??= [];
                    foreach (var t in _state.UnmatchedTracks)
                        _state.TextResults.Add(new MatchResult.NotFound(t, "Skipped"));
                }

                _state.Phase = TuiTransferPhase.CreatingPlaylist;
                _ = Task.Run(() => RunCreatePlaylistAsync(_cts.Token));
                break;
        }
    }

    private void HandleEnter()
    {
        _state.ShowHelp = false;

        var raw = _inputBuffer.ToString().Trim();
        _inputBuffer.Clear();

        if (string.IsNullOrEmpty(raw))
            return;

        if (_state.AwaitingUserToken)
        {
            HandleUserTokenInput(raw);
            return;
        }

        if (raw.StartsWith('/'))
        {
            HandleCommand(raw);
            return;
        }

        if (SpotifyUrlInfo.TryParse(raw, out var urlInfo))
        {
            if (_state.QueuedPlaylistUrls.Count > 0)
            {
                _state.QueuedPlaylistUrls.Add(urlInfo.Id);
                _logs.Append(
                    LogKind.Success,
                    $"Added to merge queue (Total: {_state.QueuedPlaylistUrls.Count}). Type /run to start."
                );
                return;
            }

            ResetTransferState();
            _state.Phase = TuiTransferPhase.FetchingPlaylist;
            _logs.Clear();
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

        _state.AwaitingUserToken = false;
        _logs.Append(LogKind.Success, "Authentication complete! Tokens cached.");
        _logs.Append(LogKind.Info, StatusSummary());
    }
}
