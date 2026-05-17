using System.Text.Json;
using System.Threading.Channels;
using Ciderfy.Apple;
using Ciderfy.Configuration;
using Ciderfy.Matching;
using Microsoft.Extensions.DependencyInjection;

namespace Ciderfy.Tui;

/// <summary>
/// Executes TUI effects and posts resulting messages back to the UI loop
/// </summary>
internal sealed class TuiEffectRunner(
    TokenCache tokenCache,
    IServiceScopeFactory scopeFactory,
    IConfigurationFolderOpener configurationFolderOpener,
    ChannelWriter<TuiMessage> messages,
    Action cancelApp,
    CancellationToken appToken
) : IDisposable
{
    private CancellationTokenSource? _currentTransferCts;

    /// <summary>
    /// Executes effects emitted by one controller update
    /// </summary>
    internal void ExecuteAll(IEnumerable<ITuiEffect> effects)
    {
        foreach (var effect in effects)
            Execute(effect);
    }

    private void Execute(ITuiEffect effect)
    {
        switch (effect)
        {
            case QuitAppEffect:
                CancelCurrentTransfer();
                cancelApp();
                break;
            case StartAuthEffect:
                StartBackgroundTask(RunAuthAsync);
                break;
            case OpenConfigEffect:
                OpenConfig();
                break;
            case CancelCurrentTransferEffect:
                CancelCurrentTransfer();
                break;
            case StartFetchPlaylistsEffect e:
                StartTransferStep(
                    e.TransferId,
                    (id, ct) => RunFetchPlaylistAsync(id, e.PlaylistIds, ct)
                );
                break;
            case StartIsrcMatchEffect e:
                StartTransferStep(
                    e.TransferId,
                    (id, ct) => RunIsrcMatchAsync(id, e.Tracks, e.Storefront, ct)
                );
                break;
            case StartTextMatchEffect e:
                StartTransferStep(
                    e.TransferId,
                    (id, ct) => RunTextMatchAsync(id, e.Tracks, e.Storefront, ct)
                );
                break;
            case StartCreatePlaylistEffect e:
                StartTransferStep(
                    e.TransferId,
                    (id, ct) => RunCreatePlaylistAsync(id, e.PlaylistName, e.AllResults, ct)
                );
                break;
            default:
                throw new InvalidOperationException($"Unknown TUI effect: {effect.GetType().Name}");
        }
    }

    private void OpenConfig()
    {
        try
        {
            configurationFolderOpener.Open();
            messages.TryWrite(new ConfigOpenedMsg(configurationFolderOpener.ConfigDirectory));
        }
        catch (Exception e) when (configurationFolderOpener.IsOpenFailure(e))
        {
            messages.TryWrite(new ConfigOpenFailedMsg(e.Message));
        }
    }

    private void StartBackgroundTask(Func<CancellationToken, Task> operation)
    {
        var task = Task.Run(() => operation(appToken), CancellationToken.None);
        ObserveBackgroundFault(task);
    }

    private void ObserveBackgroundFault(Task task)
    {
        _ = task.ContinueWith(
            t => messages.TryWrite(new FatalErrorMsg(t.Exception!.GetBaseException())),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default
        );
    }

    private void StartTransferStep(int transferId, Func<int, CancellationToken, Task> operation)
    {
        CancelCurrentTransfer();
        var operationCts = CancellationTokenSource.CreateLinkedTokenSource(appToken);
        _currentTransferCts = operationCts;

        var task = Task.Run(
            () => RunTransferStepAsync(transferId, operation, operationCts),
            CancellationToken.None
        );
        ObserveBackgroundFault(task);
    }

    private void CancelCurrentTransfer()
    {
        Interlocked.Exchange(ref _currentTransferCts, null)?.Cancel();
    }

    private async Task RunTransferStepAsync(
        int transferId,
        Func<int, CancellationToken, Task> operation,
        CancellationTokenSource operationCts
    )
    {
        try
        {
            await operation(transferId, operationCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (operationCts.IsCancellationRequested || appToken.IsCancellationRequested)
        {
            messages.TryWrite(new TransferFailedMsg(transferId, new OperationCanceledException()));
        }
        finally
        {
            Interlocked.CompareExchange(ref _currentTransferCts, null, operationCts);
            operationCts.Dispose();
        }
    }

    private async Task RunAuthAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<AppleMusicAuth>();

            await auth.GetDeveloperTokenAsync(ct).ConfigureAwait(false);
            var needsUserToken = !tokenCache.HasValidUserToken;
            messages.TryWrite(new AuthDoneMsg(needsUserToken));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsExpectedAuthException(ex))
        {
            messages.TryWrite(new AuthFailedMsg(ex));
        }
    }

    private async Task RunFetchPlaylistAsync(
        int transferId,
        IReadOnlyCollection<string> playlistIds,
        CancellationToken ct
    ) =>
        await RunTransferOperationAsync(
                transferId,
                async (services, token) =>
                {
                    var auth = services.GetRequiredService<AppleMusicAuth>();
                    var transferService = services.GetRequiredService<PlaylistTransferService>();

                    if (!tokenCache.HasValidDeveloperToken)
                        await auth.GetDeveloperTokenAsync(token).ConfigureAwait(false);

                    if (!tokenCache.HasValidUserToken)
                    {
                        messages.TryWrite(
                            new TransferFailedMsg(
                                transferId,
                                new InvalidOperationException(
                                    "User token missing. Run /auth first."
                                )
                            )
                        );
                        return;
                    }

                    var fetchTasks = playlistIds.Select(id =>
                        transferService.FetchSpotifyPlaylistAsync(id, token)
                    );
                    var playlists = await Task.WhenAll(fetchTasks).ConfigureAwait(false);
                    messages.TryWrite(new PlaylistFetchedMsg(transferId, [.. playlists]));
                },
                ct
            )
            .ConfigureAwait(false);

    private async Task RunIsrcMatchAsync(
        int transferId,
        IReadOnlyList<TrackMetadata> tracks,
        string storefront,
        CancellationToken ct
    ) =>
        await RunTransferOperationAsync(
                transferId,
                async (services, token) =>
                {
                    var transferService = services.GetRequiredService<PlaylistTransferService>();
                    var progress = new Progress<(int Current, int Total)>(p =>
                        messages.TryWrite(new IsrcProgressMsg(transferId, p.Current, p.Total))
                    );

                    var (matched, unmatched) = await transferService
                        .MatchByIsrcAsync(tracks, storefront, progress, token)
                        .ConfigureAwait(false);
                    messages.TryWrite(new IsrcDoneMsg(transferId, matched, unmatched));
                },
                ct
            )
            .ConfigureAwait(false);

    private async Task RunTextMatchAsync(
        int transferId,
        IReadOnlyList<TrackMetadata> unmatchedTracks,
        string storefront,
        CancellationToken ct
    ) =>
        await RunTransferOperationAsync(
                transferId,
                async (services, token) =>
                {
                    var transferService = services.GetRequiredService<PlaylistTransferService>();
                    var progress = new Progress<TrackMatchProgress>(p =>
                        messages.TryWrite(
                            new TextProgressMsg(
                                transferId,
                                p.Track,
                                p.CurrentIndex,
                                unmatchedTracks.Count
                            )
                        )
                    );

                    var results = await transferService
                        .MatchByTextAsync(unmatchedTracks, storefront, progress, token)
                        .ConfigureAwait(false);
                    messages.TryWrite(new TextDoneMsg(transferId, results));
                },
                ct
            )
            .ConfigureAwait(false);

    private async Task RunCreatePlaylistAsync(
        int transferId,
        string playlistName,
        IReadOnlyList<MatchResult> allResults,
        CancellationToken ct
    ) =>
        await RunTransferOperationAsync(
                transferId,
                async (services, token) =>
                {
                    var transferService = services.GetRequiredService<PlaylistTransferService>();
                    var result = await transferService
                        .CreatePlaylistAsync(playlistName, [.. allResults], token)
                        .ConfigureAwait(false);
                    messages.TryWrite(new PlaylistCreatedMsg(transferId, result, [.. allResults]));
                },
                ct
            )
            .ConfigureAwait(false);

    private async Task RunTransferOperationAsync(
        int transferId,
        Func<IServiceProvider, CancellationToken, Task> operation,
        CancellationToken ct
    )
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await operation(scope.ServiceProvider, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsExpectedTransferException(ex))
        {
            messages.TryWrite(new TransferFailedMsg(transferId, ex));
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
        _currentTransferCts?.Cancel();
        _currentTransferCts?.Dispose();
        _currentTransferCts = null;
    }
}
