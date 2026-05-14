using System.Text.Json;
using System.Threading.Channels;
using Ciderfy.Apple;
using Ciderfy.Configuration;
using Ciderfy.Matching;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Ciderfy.Tui;

/// <summary>
/// Full-screen alternate-screen TUI that drives the Spotify to Apple Music transfer pipeline
/// </summary>
internal sealed partial class TuiApp(
    TokenCache tokenCache,
    IServiceScopeFactory scopeFactory,
    IConfigurationFolderOpener configurationFolderOpener
) : IDisposable
{
    private readonly Channel<TuiMessage> _channel = Channel.CreateUnbounded<TuiMessage>();
    private readonly CancellationTokenSource _cts = new();
    private TuiController? _controller;

    private TuiController Controller =>
        _controller ??= new TuiController(
            tokenCache,
            _channel.Writer,
            () => _cts.Cancel(),
            () => StartBackgroundTask(RunAuthAsync),
            GetVisibleHelpRows,
            GetVisibleDoneRows,
            RunFetchPlaylistAsync,
            RunIsrcMatchAsync,
            RunTextMatchAsync,
            RunCreatePlaylistAsync,
            configurationFolderOpener,
            _cts.Token
        );

    /// <summary>
    /// Enters the alternate screen and runs the TUI event loop until the user quits
    /// </summary>
    public Task<int> RunAsync()
    {
        Controller.RegisterCommands();
        Controller.Logs.Append(
            LogKind.Info,
            "Paste a Spotify playlist URL to transfer, or type /help"
        );

        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            AnsiConsole.Console.AlternateScreen(() =>
            {
                Console.CursorVisible = false;
                try
                {
                    AnsiConsole
                        .Live(new Text("Loading..."))
                        .Overflow(VerticalOverflow.Crop)
                        .Cropping(VerticalOverflowCropping.Bottom)
                        .AutoClear(true)
                        // Spectre alternate-screen callback is synchronous.
                        // Block here to bridge its async live renderer.
                        .StartAsync(RunRenderLoopAsync)
                        .GetAwaiter()
                        .GetResult();
                }
                finally
                {
                    Console.CursorVisible = true;
                }
            });
            return Task.FromResult(0);
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }
    }

    private async Task RunRenderLoopAsync(LiveDisplayContext ctx)
    {
        var inputTask = Task.Run(() => ReadKeysLoop(_cts.Token), CancellationToken.None);
        ObserveBackgroundFault(inputTask);

        while (!Controller.State.QuitRequested && !_cts.IsCancellationRequested)
        {
            DrainPendingMessages();
            Controller.State.SpinnerTick++;
            Controller.State.CursorVisible = Controller.State.SpinnerTick % 8 < 5; // blinking pattern
            ctx.UpdateTarget(BuildView());
            ctx.Refresh();

            try
            {
                await Task.Delay(50, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                _channel.Writer.TryComplete();
            }
        }

        ctx.UpdateTarget(new Text(string.Empty));
        ctx.Refresh();
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _channel.Writer.TryWrite(new QuitRequestedMsg());
    }

    private void DrainPendingMessages()
    {
        while (_channel.Reader.TryRead(out var msg))
        {
            Controller.ProcessMessage(msg);
        }
    }

    private void StartBackgroundTask(Func<CancellationToken, Task> operation)
    {
        var task = Task.Run(() => operation(_cts.Token), CancellationToken.None);
        ObserveBackgroundFault(task);
    }

    private void ObserveBackgroundFault(Task task)
    {
        _ = task.ContinueWith(
            t => _channel.Writer.TryWrite(new FatalErrorMsg(t.Exception!.GetBaseException())),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default
        );
    }

    private async Task RunAuthAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<AppleMusicAuth>();

            await auth.GetDeveloperTokenAsync(ct).ConfigureAwait(false);
            var needsUserToken = !tokenCache.HasValidUserToken;
            _channel.Writer.TryWrite(new AuthDoneMsg(needsUserToken));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsExpectedAuthException(ex))
        {
            _channel.Writer.TryWrite(new AuthFailedMsg(ex));
        }
    }

    private async Task RunFetchPlaylistAsync(
        int transferId,
        IReadOnlyCollection<string> playlistIds,
        CancellationToken ct
    )
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<AppleMusicAuth>();
            var transferService =
                scope.ServiceProvider.GetRequiredService<PlaylistTransferService>();

            if (!tokenCache.HasValidDeveloperToken)
                await auth.GetDeveloperTokenAsync(ct).ConfigureAwait(false);

            if (!tokenCache.HasValidUserToken)
            {
                _channel.Writer.TryWrite(
                    new TransferFailedMsg(
                        transferId,
                        new InvalidOperationException("User token missing. Run /auth first.")
                    )
                );
                return;
            }

            var fetchTasks = playlistIds.Select(id =>
                transferService.FetchSpotifyPlaylistAsync(id, ct)
            );
            var playlists = await Task.WhenAll(fetchTasks).ConfigureAwait(false);
            _channel.Writer.TryWrite(new PlaylistFetchedMsg(transferId, [.. playlists]));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsExpectedTransferException(ex))
        {
            _channel.Writer.TryWrite(new TransferFailedMsg(transferId, ex));
        }
    }

    private async Task RunIsrcMatchAsync(
        int transferId,
        List<TrackMetadata> tracks,
        string storefront,
        CancellationToken ct
    )
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var transferService =
                scope.ServiceProvider.GetRequiredService<PlaylistTransferService>();

            var progress = new Progress<(int Current, int Total)>(p =>
                _channel.Writer.TryWrite(new IsrcProgressMsg(transferId, p.Current, p.Total))
            );

            var (matched, unmatched) = await transferService
                .MatchByIsrcAsync(tracks, storefront, progress, ct)
                .ConfigureAwait(false);
            _channel.Writer.TryWrite(new IsrcDoneMsg(transferId, matched, unmatched));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsExpectedTransferException(ex))
        {
            _channel.Writer.TryWrite(new TransferFailedMsg(transferId, ex));
        }
    }

    private async Task RunTextMatchAsync(
        int transferId,
        List<TrackMetadata> unmatchedTracks,
        string storefront,
        CancellationToken ct
    )
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var transferService =
                scope.ServiceProvider.GetRequiredService<PlaylistTransferService>();

            var progress = new Progress<TrackMatchProgress>(p =>
                _channel.Writer.TryWrite(
                    new TextProgressMsg(transferId, p.Track, p.CurrentIndex, unmatchedTracks.Count)
                )
            );

            var results = await transferService
                .MatchByTextAsync(unmatchedTracks, storefront, progress, ct)
                .ConfigureAwait(false);
            _channel.Writer.TryWrite(new TextDoneMsg(transferId, results));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsExpectedTransferException(ex))
        {
            _channel.Writer.TryWrite(new TransferFailedMsg(transferId, ex));
        }
    }

    private async Task RunCreatePlaylistAsync(
        int transferId,
        string playlistName,
        List<MatchResult> allResults,
        CancellationToken ct
    )
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var transferService =
                scope.ServiceProvider.GetRequiredService<PlaylistTransferService>();

            var result = await transferService
                .CreatePlaylistAsync(playlistName, allResults, ct)
                .ConfigureAwait(false);
            _channel.Writer.TryWrite(new PlaylistCreatedMsg(transferId, result, allResults));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsExpectedTransferException(ex))
        {
            _channel.Writer.TryWrite(new TransferFailedMsg(transferId, ex));
        }
    }

    private static bool IsExpectedAuthException(Exception ex) =>
        ex
            is HttpRequestException
                or InvalidOperationException
                or TaskCanceledException
                or JsonException;

    private static bool IsExpectedTransferException(Exception ex) =>
        ex
            is AppleMusicRateLimitException
                or AppleMusicUnauthorizedException
                or HttpRequestException
                or InvalidOperationException
                or TaskCanceledException
                or JsonException;

    public void Dispose()
    {
        _controller?.Dispose();
        _cts.Cancel();
        _channel.Writer.TryComplete();
        _cts.Dispose();
    }
}
