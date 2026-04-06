using Ciderfy.Matching;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Ciderfy.Tui;

internal sealed partial class TuiApp
{
    private const int MinStatusSectionWidth = 64;
    private const int MinWindowWidth = 24;
    private const int ContentHorizontalPadding = 4;
    private const int MinContentWidth = 20;
    private const int MinContentHeight = 4;
    private const int InputSectionHeight = 4;
    private const int SeparatorHeight = 1;
    private const int FooterHeight = 1;
    private const int PlaylistConfirmationHeight = 7;
    private const int SpinnerSectionHeight = 1;
    private const int ProgressSectionHeight = 2;
    private const int QueuePanelChromeHeight = 2;
    private const int QueuePanelGapHeight = 1;
    private const int MinQueueLogHeight = 2;
    private const int MinLogHeight = 4;
    private const int HelpViewChromeHeight = 3 + 1 + 1;
    private const int OuterPanelBorderHeight = 2;

    private int CurrentFixedChromeHeight =>
        GetFixedChromeHeight(
            Components.GetBannerHeight(CurrentContentWidth),
            GetStatusSectionHeight(CurrentContentWidth)
        );

    private static int CurrentWindowWidth => Math.Max(MinWindowWidth, Console.WindowWidth);

    private static int CurrentContentWidth => GetContentWidth(CurrentWindowWidth);

    private bool ShowInput =>
        !_state.ShowHelp
        && (
            _state.AwaitingUserToken
            || _state.Phase is TuiTransferPhase.Idle or TuiTransferPhase.Done
        );

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
        var width = CurrentWindowWidth;
        var height = Console.WindowHeight;
        var contentWidth = GetContentWidth(width);
        var bannerHeight = Components.GetBannerHeight(contentWidth);
        var statusSectionHeight = GetStatusSectionHeight(contentWidth);
        var contentHeight = Math.Max(
            MinContentHeight,
            height - GetFixedChromeHeight(bannerHeight, statusSectionHeight)
        );

        var rows = new List<Layout>
        {
            new Layout(RegionBanner).Size(bannerHeight),
            new Layout(RegionSeparator).Size(SeparatorHeight),
        };

        if (statusSectionHeight > 0)
            rows.Add(new Layout(RegionBadgesAndStepper).Size(statusSectionHeight));

        rows.Add(new Layout(RegionMain));
        rows.Add(
            new Layout(RegionFooter).Size(
                ShowInput ? InputSectionHeight + FooterHeight : FooterHeight
            )
        );

        var layout = new Layout(RegionRoot).SplitRows([.. rows]);

        layout[RegionBanner].Update(Components.RenderBanner(contentWidth));
        layout[RegionSeparator].Update(new Rule { Style = new Style(Theme.GrayColor) });

        if (statusSectionHeight > 0)
            layout[RegionBadgesAndStepper].Update(BuildStatusSection());

        layout[RegionMain].Update(BuildMainContent(contentWidth, contentHeight));

        layout[RegionFooter].Update(new Rows(BuildFooterItems(contentWidth, contentHeight)));

        return new Panel(layout)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Theme.GrayColor),
            Padding = new Padding(1, 0),
            Width = width,
            Height = height,
        };
    }

    private int GetFixedChromeHeight(int bannerHeight, int statusSectionHeight) =>
        bannerHeight
        + SeparatorHeight
        + statusSectionHeight
        + FooterHeight
        + (ShowInput ? InputSectionHeight : 0)
        + OuterPanelBorderHeight;

    private int GetStatusSectionHeight(int contentWidth) =>
        ShouldShowStatusSection(contentWidth) ? 4 : 0;

    private static int GetContentWidth(int width) =>
        Math.Max(MinContentWidth, width - ContentHorizontalPadding);

    private List<IRenderable> BuildFooterItems(int contentWidth, int contentHeight)
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
                _state.ShowHelp,
                ShouldShowScrollActions(contentHeight)
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

    private Rows BuildStatusSection()
    {
        var badges = Components.RenderStatusBadges(
            tokenCache.HasValidDeveloperToken,
            tokenCache.HasValidUserToken,
            _state.Storefront,
            _state.NextPlaylistName
        );

        return new Rows(
            badges,
            new Text(string.Empty),
            Components.RenderStepper(_state.Phase),
            new Text(string.Empty)
        );
    }

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
            return BuildHelpView(contentHeight);

        if (_state.QueuedPlaylistUrls.Count > 0 && _state.Phase is TuiTransferPhase.Idle)
            return BuildQueuedPlaylistsView(width, contentHeight);

        var logs = Components.RenderLogArea(_logs, width, logHeight);

        if (progressSection is not null)
            return new Rows(logs, new Text(string.Empty), progressSection);

        return logs;
    }

    private Align BuildHelpView(int contentHeight)
    {
        var visibleRows = GetVisibleHelpRows(contentHeight);
        var table = Components.RenderHelpTable(_state.ScrollOffset, visibleRows);
        var hint = Components.RenderHelpScrollHint(
            _state.ScrollOffset,
            Components.HelpEntryCount,
            visibleRows
        );

        return new Align(
            new Rows(Align.Center(table), new Text(string.Empty), Align.Center(hint)),
            HorizontalAlignment.Center,
            VerticalAlignment.Middle
        );
    }

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
        var visibleRows = GetVisibleDoneRows(contentHeight);
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

    private bool ShouldShowStatusSection(int contentWidth) =>
        !_state.ShowHelp && contentWidth >= MinStatusSectionWidth;

    private bool ShouldShowScrollActions(int contentHeight)
    {
        if (_state.ShowHelp && _state.Phase is TuiTransferPhase.Idle)
            return Components.HelpEntryCount > GetVisibleHelpRows(contentHeight);

        if (_state.Phase is not TuiTransferPhase.Done || _state.AllResults is null)
            return false;

        return _state.AllResults.Count > GetVisibleDoneRows(contentHeight);
    }

    private static int GetVisibleRows(int contentHeight, int chromeHeight) =>
        Math.Max(3, contentHeight - chromeHeight);

    private static int GetVisibleDoneRows(int contentHeight) =>
        GetVisibleRows(contentHeight, DoneViewChromeHeight);

    private int GetVisibleDoneRows() =>
        GetVisibleDoneRows(Console.WindowHeight - CurrentFixedChromeHeight);

    private static int GetVisibleHelpRows(int contentHeight) =>
        GetVisibleRows(contentHeight, HelpViewChromeHeight);

    private int GetVisibleHelpRows() =>
        GetVisibleHelpRows(Console.WindowHeight - CurrentFixedChromeHeight);
}
