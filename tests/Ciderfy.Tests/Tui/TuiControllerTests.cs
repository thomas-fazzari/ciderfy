using Ciderfy.Apple;
using Ciderfy.Matching;
using Ciderfy.Tests.Fakers;
using Ciderfy.Tui;
using Xunit;

namespace Ciderfy.Tests.Tui;

public sealed class TuiControllerTests
{
    private static readonly ConsoleKeyInfo _enterKey = new(
        keyChar: '\r',
        key: ConsoleKey.Enter,
        shift: false,
        alt: false,
        control: false
    );
    private static readonly ConsoleKeyInfo _escapeKey = new(
        keyChar: '\u001b',
        key: ConsoleKey.Escape,
        shift: false,
        alt: false,
        control: false
    );
    private static readonly ConsoleKeyInfo _yesKey = new(
        keyChar: 'y',
        key: ConsoleKey.Y,
        shift: false,
        alt: false,
        control: false
    );
    private static readonly ConsoleKeyInfo _noKey = new(
        keyChar: 'n',
        key: ConsoleKey.N,
        shift: false,
        alt: false,
        control: false
    );

    [Fact]
    public void SpotifyUrl_StartsFetchEffect()
    {
        var controller = CreateController();

        var effects = Submit(
            controller: controller,
            input: "https://open.spotify.com/playlist/playlist-1"
        );

        var fetch = Assert.IsType<StartFetchPlaylistsEffect>(Assert.Single(effects));
        Assert.Equal(1, fetch.TransferId);
        Assert.Equal(["playlist-1"], fetch.PlaylistIds);
        Assert.Equal(TuiTransferPhase.FetchingPlaylist, controller.State.Phase);
        Assert.Contains(
            Logs(controller: controller),
            entry => entry is { Kind: LogKind.Info, Message: "Starting transfer..." }
        );
    }

    [Fact]
    public void PlaylistConfirmation_StartsIsrcMatchEffect()
    {
        var controller = CreateController();
        var fetch = Assert.IsType<StartFetchPlaylistsEffect>(
            Assert.Single(Submit(controller: controller, input: "spotify:playlist:playlist-1"))
        );

        var playlist = PlaylistFaker.WithNameAndTracks("Spotify List", trackCount: 1).Generate();
        Assert.Empty(
            controller.ProcessMessage(
                msg: new PlaylistFetchedMsg(TransferId: fetch.TransferId, Playlists: [playlist])
            )
        );

        var effects = Press(controller: controller, key: _enterKey);

        var isrc = Assert.IsType<StartIsrcMatchEffect>(Assert.Single(effects));
        Assert.Equal(fetch.TransferId, isrc.TransferId);
        Assert.Equal("us", isrc.Storefront);
        Assert.Single(isrc.Tracks);
        Assert.Equal(TuiTransferPhase.ResolvingIsrc, controller.State.Phase);
    }

    [Fact]
    public void IsrcDone_WithAllTracksMatched_StartsCreatePlaylistEffect()
    {
        var (controller, transferId, track) = StartConfirmedTransfer();
        var match = new MatchResult.Matched(
            SpotifyTrack: track,
            AppleTrack: AppleTrackFaker.Default.Generate(),
            Method: MatchMethod.Isrc,
            Confidence: 1.0
        );

        var effects = controller.ProcessMessage(
            msg: new IsrcDoneMsg(TransferId: transferId, Matched: [match], Unmatched: [])
        );

        var create = Assert.IsType<StartCreatePlaylistEffect>(Assert.Single(effects));
        Assert.Equal(transferId, create.TransferId);
        Assert.Equal("Spotify List", create.PlaylistName);
        Assert.Same(match, Assert.Single(create.AllResults));
        Assert.Equal(TuiTransferPhase.CreatingPlaylist, controller.State.Phase);
    }

    [Fact]
    public void IsrcDone_WithUnmatchedTracks_WaitsForTextConfirmation()
    {
        var (controller, transferId, track) = StartConfirmedTransfer();

        var effects = controller.ProcessMessage(
            msg: new IsrcDoneMsg(TransferId: transferId, Matched: [], Unmatched: [track])
        );

        Assert.Empty(effects);
        Assert.Equal(TuiTransferPhase.ConfirmTextMatch, controller.State.Phase);

        effects = Press(controller: controller, key: _yesKey);

        var text = Assert.IsType<StartTextMatchEffect>(Assert.Single(effects));
        Assert.Equal(transferId, text.TransferId);
        Assert.Same(track, Assert.Single(text.Tracks));
        Assert.Equal(TuiTransferPhase.TextMatching, controller.State.Phase);
    }

    [Fact]
    public void TextFallbackSkip_StartsCreatePlaylistWithSkippedResult()
    {
        var (controller, transferId, track) = StartConfirmedTransfer();
        _ = controller.ProcessMessage(
            msg: new IsrcDoneMsg(TransferId: transferId, Matched: [], Unmatched: [track])
        );

        var effects = Press(controller: controller, key: _noKey);

        var create = Assert.IsType<StartCreatePlaylistEffect>(Assert.Single(effects));
        var notFound = Assert.IsType<MatchResult.NotFound>(Assert.Single(create.AllResults));
        Assert.Same(track, notFound.SpotifyTrack);
        Assert.Equal("Skipped", notFound.Reason);
        Assert.Equal(TuiTransferPhase.CreatingPlaylist, controller.State.Phase);
    }

    [Fact]
    public void StaleTransferMessage_IsIgnored()
    {
        var controller = CreateController();
        var first = Assert.IsType<StartFetchPlaylistsEffect>(
            Assert.Single(Submit(controller: controller, input: "spotify:playlist:first"))
        );
        Assert.IsType<CancelCurrentTransferEffect>(
            Assert.Single(Press(controller: controller, key: _escapeKey))
        );
        var second = Assert.IsType<StartFetchPlaylistsEffect>(
            Assert.Single(Submit(controller: controller, input: "spotify:playlist:second"))
        );

        var effects = controller.ProcessMessage(
            msg: new PlaylistFetchedMsg(
                TransferId: first.TransferId,
                Playlists: [PlaylistFaker.WithNameAndTracks("Old", trackCount: 1).Generate()]
            )
        );

        Assert.Empty(effects);
        Assert.True(second.TransferId > first.TransferId);
        Assert.Equal(TuiTransferPhase.FetchingPlaylist, controller.State.Phase);
        Assert.Empty(controller.State.TransferTracks);
    }

    [Fact]
    public void Commands_EmitSideEffectRequests()
    {
        var controller = CreateController();

        Assert.IsType<StartAuthEffect>(
            Assert.Single(Submit(controller: controller, input: "/auth "))
        );
        Assert.IsType<OpenConfigEffect>(
            Assert.Single(Submit(controller: controller, input: "/config"))
        );
    }

    [Fact]
    public void Enter_WithPartialCommandSuggestion_CompletesSuggestion()
    {
        var controller = CreateController();

        var effects = Submit(controller: controller, input: "/au");

        Assert.Empty(effects);
        Assert.Equal("/auth", controller.InputBuffer.ToString());
    }

    [Fact]
    public void Enter_WithArgumentCommandSuggestion_CompletesWithSeparator()
    {
        var controller = CreateController();

        var effects = Submit(controller: controller, input: "/store");

        Assert.Empty(effects);
        Assert.Equal("/storefront ", controller.InputBuffer.ToString());
    }

    [Fact]
    public void Enter_WithExactCommand_SubmitsCommand()
    {
        var controller = CreateController();

        var effects = Submit(controller: controller, input: "/auth");

        Assert.IsType<StartAuthEffect>(Assert.Single(effects));
        Assert.Empty(controller.InputBuffer.ToString());
    }

    [Fact]
    public void Enter_WithExactCommandAndArgumentSuggestion_SubmitsExactCommand()
    {
        var controller = CreateController();
        controller.State.NextPlaylistName = "Custom";

        var effects = Submit(controller: controller, input: "/name");

        Assert.Empty(effects);
        Assert.Null(controller.State.NextPlaylistName);
        Assert.Empty(controller.InputBuffer.ToString());
    }

    [Fact]
    public void Enter_WithResetAuthCommandSuggestion_CompletesSuggestion()
    {
        var controller = CreateController();

        var effects = Submit(controller: controller, input: "/reset");

        Assert.Empty(effects);
        Assert.Equal("/reset-auth", controller.InputBuffer.ToString());
    }

    [Fact]
    public void Enter_WithCompletedResetAuthSuggestion_ClearsTokensAndStartsAuth()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(cacheDirectory);
        try
        {
            var tokenCache = new TokenCache(Path.Combine(cacheDirectory, "tokens.json"))
            {
                DeveloperToken = "developer-token",
                DeveloperTokenExpiry = DateTimeOffset.UtcNow.AddHours(1),
                UserToken = "user-token",
                UserTokenExpiry = DateTimeOffset.UtcNow.AddHours(1),
            };
            var controller = CreateController(tokenCache);

            Assert.Empty(Submit(controller: controller, input: "/reset"));
            var effects = Press(controller: controller, key: _enterKey);

            Assert.IsType<StartAuthEffect>(Assert.Single(effects));
            Assert.Null(tokenCache.DeveloperToken);
            Assert.Null(tokenCache.DeveloperTokenExpiry);
            Assert.Null(tokenCache.UserToken);
            Assert.Null(tokenCache.UserTokenExpiry);
            Assert.Empty(controller.InputBuffer.ToString());
            Assert.Contains(
                Logs(controller: controller),
                entry => entry is { Kind: LogKind.Warning, Message: "Tokens cleared." }
            );
            Assert.Contains(
                Logs(controller: controller),
                entry => entry is { Kind: LogKind.Info, Message: "Authenticating..." }
            );
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public void Enter_WithTrailingSeparator_SubmitsCommand()
    {
        var controller = CreateController();

        var effects = Submit(controller: controller, input: "/auth ");

        Assert.IsType<StartAuthEffect>(Assert.Single(effects));
        Assert.Empty(controller.InputBuffer.ToString());
    }

    [Fact]
    public void StaleTransferFailure_IsIgnored()
    {
        var controller = CreateController();
        var first = Assert.IsType<StartFetchPlaylistsEffect>(
            Assert.Single(Submit(controller: controller, input: "spotify:playlist:first"))
        );
        Assert.IsType<CancelCurrentTransferEffect>(
            Assert.Single(Press(controller: controller, key: _escapeKey))
        );
        var second = Assert.IsType<StartFetchPlaylistsEffect>(
            Assert.Single(Submit(controller: controller, input: "spotify:playlist:second"))
        );

        var effects = controller.ProcessMessage(
            msg: new TransferFailedMsg(
                TransferId: first.TransferId,
                Error: new InvalidOperationException("old failure")
            )
        );

        Assert.Empty(effects);
        Assert.True(second.TransferId > first.TransferId);
        Assert.Equal(TuiTransferPhase.FetchingPlaylist, controller.State.Phase);
        Assert.DoesNotContain(
            Logs(controller: controller),
            entry => entry.Message == "old failure"
        );
    }

    private static (
        TuiController Controller,
        int TransferId,
        TrackMetadata Track
    ) StartConfirmedTransfer()
    {
        var controller = CreateController();
        var fetch = Assert.IsType<StartFetchPlaylistsEffect>(
            Assert.Single(Submit(controller: controller, input: "spotify:playlist:playlist-1"))
        );
        var playlist = PlaylistFaker.WithNameAndTracks("Spotify List", trackCount: 1).Generate();
        _ = controller.ProcessMessage(
            msg: new PlaylistFetchedMsg(TransferId: fetch.TransferId, Playlists: [playlist])
        );
        var track = Assert.Single(controller.State.TransferTracks);
        _ = Press(controller: controller, key: _enterKey);
        return (controller, fetch.TransferId, track);
    }

    private static TuiController CreateController() =>
        CreateController(tokenCache: new TokenCache());

    private static TuiController CreateController(TokenCache tokenCache) =>
        new(tokenCache: tokenCache, getVisibleHelpRows: () => 5, getVisibleDoneRows: () => 5);

    private static IReadOnlyList<ITuiEffect> Submit(TuiController controller, string input)
    {
        controller.InputBuffer.Append(value: input);
        return Press(controller: controller, key: _enterKey);
    }

    private static IReadOnlyList<ITuiEffect> Press(TuiController controller, ConsoleKeyInfo key) =>
        controller.ProcessMessage(msg: new KeyPressedMsg(Key: key));

    private static LogEntry[] Logs(TuiController controller) =>
        controller.Logs.GetVisible(height: 20).ToArray();
}
