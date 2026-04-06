using Ciderfy.Matching;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Ciderfy.Tui;

internal sealed partial class TuiApp
{
    private const int MinWindowWidth = 24;
    private const int ContentHorizontalPadding = 4;
    private const int MinContentWidth = 20;
    private const int MinContentHeight = 4;
    private const int InputSectionHeight = 4;
    private const int BannerHeight = 6;
    private const int SeparatorHeight = 1;
    private const int StatusSectionHeight = 4;
    private const int FooterHeight = 1;
    private const int PlaylistConfirmationHeight = 7;
    private const int SpinnerSectionHeight = 1;
    private const int ProgressSectionHeight = 2;
    private const int QueuePanelChromeHeight = 2;
    private const int QueuePanelGapHeight = 1;
    private const int MinQueueLogHeight = 2;
    private const int MinLogHeight = 4;

    // Fixed vertical height used for calculating manual internal limits
    private int CurrentFixedChromeHeight =>
        BannerHeight
        + SeparatorHeight
        + StatusSectionHeight
        + FooterHeight
        + (ShowInput ? InputSectionHeight : 0)
        + 2; // Spacing between major sections

    private bool ShowInput =>
        _state.AwaitingUserToken || _state.Phase is TuiTransferPhase.Idle or TuiTransferPhase.Done;

    // Lines consumed within the Done view by non-table elements.
    // Summary panel (3) + Gap(1) + Table header and borders (3) + Gap (1) + Hint (1)
    private const int DoneViewChromeHeight = 3 + 1 + 3 + 1 + 1;

    // Layout regions
    private const string RegionRoot = "Root";
    private const string RegionBanner = "Banner";
    private const string RegionSeparator = "Separator";
    private const string RegionBadgesAndStepper = "BadgesAndStepper";
    private const string RegionMain = "Main";
    private const string RegionFooter = "Footer";

    private Panel BuildView()
    {
        var width = Math.Max(MinWindowWidth, Console.WindowWidth);
        var height = Console.WindowHeight;
        var contentWidth = Math.Max(MinContentWidth, width - ContentHorizontalPadding);
        var contentHeight = Math.Max(MinContentHeight, height - CurrentFixedChromeHeight);

        var layout = new Layout(RegionRoot).SplitRows(
            new Layout(RegionBanner).Size(BannerHeight),
            new Layout(RegionSeparator).Size(SeparatorHeight),
            new Layout(RegionBadgesAndStepper).Size(StatusSectionHeight),
            new Layout(RegionMain),
            new Layout(RegionFooter).Size(
                ShowInput ? InputSectionHeight + FooterHeight : FooterHeight
            )
        );

        layout[RegionBanner].Update(Components.RenderBanner(contentWidth));
        layout[RegionSeparator].Update(new Rule { Style = new Style(Theme.GrayColor) });

        layout[RegionBadgesAndStepper].Update(BuildStatusSection());
        layout[RegionMain].Update(BuildMainContent(contentWidth, contentHeight));

        layout[RegionFooter].Update(new Rows(BuildFooterItems(contentWidth)));

        return new Panel(layout)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Theme.GrayColor),
            Padding = new Padding(1, 0),
            Width = width,
            Height = height,
        };
    }

    private List<IRenderable> BuildFooterItems(int contentWidth)
    {
        var items = new List<IRenderable>(ShowInput ? 3 : 1);

        if (ShowInput)
        {
            items.Add(BuildInputRenderable(contentWidth));
            items.Add(new Text(string.Empty));
        }

        items.Add(
            Components.RenderContextualFooter(
                _state.Phase,
                _state.AwaitingUserToken,
                _state.ShowHelp
            )
        );
        return items;
    }

    private IRenderable BuildInputRenderable(int contentWidth) =>
        _state.AwaitingUserToken
            ? Components.RenderInput(
                _inputBuffer.ToString(),
                _state.CursorVisible,
                contentWidth,
                prompt: "token>",
                placeholder: null
            )
            : Components.RenderInput(_inputBuffer.ToString(), _state.CursorVisible, contentWidth);

    private Rows BuildStatusSection() =>
        new(
            Components.RenderStatusBadges(
                tokenCache.HasValidDeveloperToken,
                tokenCache.HasValidUserToken,
                _state.Storefront,
                _state.NextPlaylistName
            ),
            new Text(string.Empty),
            Components.RenderStepper(_state.Phase),
            new Text(string.Empty)
        );

    private IRenderable BuildMainContent(int contentWidth, int contentHeight) =>
        _state.Phase switch
        {
            TuiTransferPhase.Done => RenderDoneView(contentWidth, contentHeight),
            _ => RenderActiveView(contentWidth, contentHeight),
        };

    private IRenderable RenderActiveView(int width, int contentHeight)
    {
        var progressLines = 0;
        IRenderable? progressSection = null;

        switch (_state.Phase)
        {
            case TuiTransferPhase.ConfirmPlaylist:
                progressLines = PlaylistConfirmationHeight;
                progressSection = Components.RenderPlaylistConfirmation(
                    _state.PlaylistName,
                    _state.TransferTracks?.Count ?? 0,
                    _state.Storefront
                );
                break;
            case TuiTransferPhase.FetchingPlaylist:
                progressLines = SpinnerSectionHeight;
                progressSection = Components.RenderSpinnerLine(
                    "Fetching Spotify playlist...",
                    _state.SpinnerTick
                );
                break;
            case TuiTransferPhase.ResolvingIsrc
                when _state.ProgressTotal > 0 && _state.ProgressCurrent >= _state.ProgressTotal:
                progressLines = SpinnerSectionHeight;
                progressSection = Components.RenderSpinnerLine(
                    "Matching against Apple Music...",
                    _state.SpinnerTick
                );
                break;
            case TuiTransferPhase.ResolvingIsrc:
                progressLines = ProgressSectionHeight;
                progressSection = Components.RenderProgressBar(
                    $"Resolving ISRCs via Deezer ({_state.ProgressCurrent}/{_state.ProgressTotal})",
                    _state.ProgressTotal > 0
                        ? (double)_state.ProgressCurrent / _state.ProgressTotal
                        : 0,
                    width
                );
                break;
            case TuiTransferPhase.ConfirmTextMatch:
                progressLines = SpinnerSectionHeight;
                progressSection = Components.RenderConfirmPrompt(
                    _state.UnmatchedTracks?.Count ?? 0
                );
                break;
            case TuiTransferPhase.TextMatching:
                progressLines = ProgressSectionHeight;
                progressSection = Components.RenderProgressBar(
                    string.IsNullOrEmpty(_state.ProgressLabel)
                        ? "Text matching..."
                        : $"Matching: {_state.ProgressLabel}",
                    _state.ProgressTotal > 0
                        ? (double)_state.ProgressCurrent / _state.ProgressTotal
                        : 0,
                    width
                );
                break;
            case TuiTransferPhase.CreatingPlaylist:
                progressLines = SpinnerSectionHeight;
                progressSection = Components.RenderSpinnerLine(
                    "Creating Apple Music playlist...",
                    _state.SpinnerTick
                );
                break;
        }

        var logHeight =
            progressLines > 0
                ? Math.Max(MinLogHeight, contentHeight - progressLines - QueuePanelGapHeight)
                : contentHeight;

        if (_state.ShowHelp && _state.Phase is TuiTransferPhase.Idle)
            return BuildHelpView();

        if (_state.QueuedPlaylistUrls.Count > 0 && _state.Phase is TuiTransferPhase.Idle)
            return BuildQueuedPlaylistsView(width, contentHeight);

        var logs = Components.RenderLogArea(_logs, width, logHeight);

        if (progressSection is not null)
            return new Rows(logs, new Text(string.Empty), progressSection);

        return logs;
    }

    private static Align BuildHelpView() =>
        new(Components.RenderHelpTable(), HorizontalAlignment.Center, VerticalAlignment.Middle);

    private Rows BuildQueuedPlaylistsView(int width, int contentHeight)
    {
        var queuePanelHeight = QueuePanelChromeHeight + GetVisibleQueueItemsCount();
        var queueLogHeight = Math.Max(
            MinQueueLogHeight,
            contentHeight - queuePanelHeight - QueuePanelGapHeight
        );
        var logArea = Components.RenderLogArea(_logs, width, queueLogHeight);

        return new Rows(
            logArea,
            new Text(string.Empty),
            Components.RenderQueuePanel(_state.QueuedPlaylistUrls)
        );
    }

    private int GetVisibleQueueItemsCount()
    {
        var visibleQueueItems = Math.Min(
            _state.QueuedPlaylistUrls.Count,
            Components.MaxVisibleQueuedPlaylists
        );
        var hasExtraItems =
            _state.QueuedPlaylistUrls.Count > Components.MaxVisibleQueuedPlaylists ? 1 : 0;
        return visibleQueueItems + hasExtraItems;
    }

    private IRenderable RenderDoneView(int width, int contentHeight)
    {
        if (_state.AllResults is null || _state.TransferTracks is null)
            return new Text("No results");

        var matched = _state.AllResults.OfType<MatchResult.Matched>().Count();
        var total = _state.TransferTracks.Count;
        var notFound = total - matched;

        var summary = Components.RenderSummaryPanel(_state.PlaylistName, matched, total, notFound);
        var visibleRows = Math.Max(3, contentHeight - DoneViewChromeHeight);
        var table = Components.RenderResultsTable(
            _state.AllResults,
            _state.ScrollOffset,
            visibleRows,
            width
        );
        var hint = Components.RenderScrollHint(
            _state.ScrollOffset,
            _state.AllResults.Count,
            visibleRows
        );

        return new Rows(summary, new Text(string.Empty), table, new Text(string.Empty), hint);
    }

    private string StatusSummary()
    {
        var dev = tokenCache.HasValidDeveloperToken ? "valid" : "missing";
        var user = tokenCache.HasValidUserToken ? "valid" : "missing";
        return $"Developer token: {dev}  |  User token: {user}  |  Storefront: {_state.Storefront}";
    }
}
