using Ciderfy.Spotify;

namespace Ciderfy.Tui;

internal sealed partial class TuiApp
{
    private void HandleCommand(string raw)
    {
        var parts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0];
        var arg = parts.Length > 1 ? parts[1].Trim() : null;

        if (_commands.TryExecute(cmd, arg))
            return;

#pragma warning disable CA1308
        _logs.Append(LogKind.Error, $"Unknown command: {cmd.ToLowerInvariant()}. Type /help");
#pragma warning restore CA1308
    }

    private void EnsureCommandsRegistered()
    {
        _commands.Register(HandleQuitCommand, "/quit", "/exit", "/q");
        _commands.Register(HandleHelpCommand, "/help", "/h");
        _commands.Register(HandleStatusCommand, "/status");
        _commands.Register(HandleStorefrontCommand, "/storefront", "/sf");
        _commands.Register(HandleNameCommand, "/name");
        _commands.Register(HandleAuthCommand, "/auth");
        _commands.Register(HandleAddCommand, "/add");
        _commands.Register(HandleRunCommand, "/run");
    }

    private void HandleQuitCommand() => RequestQuit();

    private void HandleHelpCommand()
    {
        _state.ShowHelp = !_state.ShowHelp;
        _state.ScrollOffset = 0;
    }

    private void HandleStatusCommand() => _logs.Append(LogKind.Info, StatusSummary());

    private void HandleStorefrontCommand(string? argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            _logs.Append(LogKind.Info, $"Storefront: {_state.Storefront}. Usage: /storefront fr");
            return;
        }

#pragma warning disable CA1308
        _state.Storefront = argument.ToLowerInvariant();
#pragma warning restore CA1308
        _logs.Append(LogKind.Success, $"Storefront set to {_state.Storefront}");
    }

    private void HandleNameCommand(string? argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            _state.NextPlaylistName = null;
            _logs.Append(LogKind.Success, "Playlist name override cleared");
            return;
        }

        _state.NextPlaylistName = argument;
        _logs.Append(LogKind.Success, $"Next playlist name set to \"{_state.NextPlaylistName}\"");
    }

    private void HandleAuthCommand(string? argument)
    {
        _logs.Clear();

        if ("reset".Equals(argument, StringComparison.OrdinalIgnoreCase))
        {
            tokenCache.Clear();
            _logs.Append(LogKind.Warning, "Tokens cleared.");
        }

        _logs.Append(LogKind.Info, "Authenticating...");
        _ = Task.Run(() => RunAuthAsync(_cts.Token), _cts.Token);
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
            if (
                SpotifyUrlInfo.TryParse(url, out var urlInfo)
                && !_state.QueuedPlaylistUrls.Contains(urlInfo.Id)
            )
            {
                _state.QueuedPlaylistUrls.Add(urlInfo.Id);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            _logs.Append(
                LogKind.Success,
                $"Added {addedCount} playlists to queue. ({_state.QueuedPlaylistUrls.Count} total)"
            );
            _logs.Append(LogKind.Info, "Type /run to start merging.");
        }
        else
        {
            _logs.Append(LogKind.Error, "No valid Spotify URLs found.");
        }
    }

    private void HandleRunCommand()
    {
        if (_state.QueuedPlaylistUrls.Count == 0)
        {
            _logs.Append(LogKind.Error, "Queue is empty. Add playlists with /add <url>");
            return;
        }

        var playlistIdsToFetch = _state.QueuedPlaylistUrls.ToList();
        _state.QueuedPlaylistUrls.Clear();

        ResetTransferState();
        _state.Phase = TuiTransferPhase.FetchingPlaylist;
        _logs.Clear();
        _logs.Append(
            LogKind.Info,
            $"Starting transfer of {playlistIdsToFetch.Count} merged playlists..."
        );
        _ = Task.Run(() => RunFetchPlaylistAsync(playlistIdsToFetch, _cts.Token), _cts.Token);
    }
}
