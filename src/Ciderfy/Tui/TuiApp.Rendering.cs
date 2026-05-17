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
    private const int HelpSectionChromeHeight = 3 + 1 + 1;
    private const int OuterPanelBorderHeight = 2;

    private int CurrentFixedChromeHeight =>
        GetCurrentFixedChromeHeight(Console.WindowHeight, CurrentContentWidth);

    private static int CurrentWindowWidth => Math.Max(MinWindowWidth, Console.WindowWidth);

    private static int CurrentContentWidth => GetContentWidth(CurrentWindowWidth);

    private bool ShowInput
    {
        get
        {
            var state = Controller.State;
            return !state.ShowHelp
                && (
                    state.AwaitingUserToken
                    || state.Phase is TuiTransferPhase.Idle or TuiTransferPhase.Done
                );
        }
    }

    /// <summary>
    /// Lines consumed within the Done view by non-table elements
    /// </summary>
    /// <remarks>
    /// Summary panel (3) + Gap(1) + Table header and borders (3) + Gap (1) + Hint (1)
    /// </remarks>
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
        var bannerHeight = BannerSection.GetHeight(contentWidth);
        var statusSectionHeight = GetStatusSectionHeight(contentWidth);
        var (suggestions, selectedSuggestionIndex) = GetVisibleCommandSuggestions(
            height,
            bannerHeight,
            statusSectionHeight
        );
        var footerHeight = GetFooterHeight(suggestions.Count);
        var contentHeight = Math.Max(
            MinContentHeight,
            height - GetFixedChromeHeight(bannerHeight, statusSectionHeight, footerHeight)
        );

        var rows = new List<Layout>
        {
            new Layout(RegionBanner).Size(bannerHeight),
            new Layout(RegionSeparator).Size(SeparatorHeight),
        };

        if (statusSectionHeight > 0)
            rows.Add(new Layout(RegionBadgesAndStepper).Size(statusSectionHeight));

        rows.Add(new Layout(RegionMain));
        rows.Add(new Layout(RegionFooter).Size(footerHeight));

        var layout = new Layout(RegionRoot).SplitRows([.. rows]);

        layout[RegionBanner].Update(BannerSection.Render(contentWidth));
        layout[RegionSeparator].Update(new Rule { Style = new Style(Theme.GrayColor) });

        if (statusSectionHeight > 0)
            layout[RegionBadgesAndStepper].Update(BuildStatusSection());

        layout[RegionMain].Update(BuildMainContent(contentWidth, contentHeight));

        layout[RegionFooter]
            .Update(
                new Rows(
                    BuildFooterItems(
                        contentWidth,
                        contentHeight,
                        suggestions,
                        selectedSuggestionIndex
                    )
                )
            );

        return new Panel(layout)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Theme.GrayColor),
            Padding = new Padding(1, 0),
            Width = width,
            Height = height,
        };
    }

    private static int GetFixedChromeHeight(
        int bannerHeight,
        int statusSectionHeight,
        int footerHeight
    ) =>
        bannerHeight
        + SeparatorHeight
        + statusSectionHeight
        + footerHeight
        + OuterPanelBorderHeight;

    private int GetCurrentFixedChromeHeight(int windowHeight, int contentWidth)
    {
        var bannerHeight = BannerSection.GetHeight(contentWidth);
        var statusSectionHeight = GetStatusSectionHeight(contentWidth);
        var suggestionCount = GetVisibleCommandSuggestions(
            windowHeight,
            bannerHeight,
            statusSectionHeight
        ).Suggestions.Count;

        return GetFixedChromeHeight(
            bannerHeight,
            statusSectionHeight,
            GetFooterHeight(suggestionCount)
        );
    }

    private int GetFooterHeight(int suggestionCount) =>
        FooterHeight
        + (
            ShowInput ? InputSectionHeight + CommandSuggestionSection.GetHeight(suggestionCount) : 0
        );

    private (
        IReadOnlyList<TuiCommandSuggestion> Suggestions,
        int SelectedIndex
    ) GetVisibleCommandSuggestions(int windowHeight, int bannerHeight, int statusSectionHeight)
    {
        var suggestions = Controller.CommandSuggestions;
        if (!ShowInput || suggestions.Count == 0)
            return ([], 0);

        var mandatoryChromeHeight =
            bannerHeight
            + SeparatorHeight
            + statusSectionHeight
            + FooterHeight
            + InputSectionHeight
            + OuterPanelBorderHeight;
        var availableHeight = windowHeight - mandatoryChromeHeight - MinContentHeight;
        var visibleCount = CommandSuggestionSection.GetVisibleCount(
            suggestions.Count,
            availableHeight
        );

        if (visibleCount == 0)
            return ([], 0);

        var selectedIndex = Controller.SelectedCommandSuggestionIndex;
        var firstVisibleIndex = Math.Clamp(
            selectedIndex - visibleCount + 1,
            0,
            suggestions.Count - visibleCount
        );

        return (
            suggestions.Skip(firstVisibleIndex).Take(visibleCount).ToArray(),
            selectedIndex - firstVisibleIndex
        );
    }

    private int GetStatusSectionHeight(int contentWidth) =>
        ShouldShowStatusSection(contentWidth) ? 4 : 0;

    private static int GetContentWidth(int width) =>
        Math.Max(MinContentWidth, width - ContentHorizontalPadding);

    private List<IRenderable> BuildFooterItems(
        int contentWidth,
        int contentHeight,
        IReadOnlyList<TuiCommandSuggestion> suggestions,
        int selectedSuggestionIndex
    )
    {
        var state = Controller.State;
        var items = new List<IRenderable>(ShowInput ? 3 : 1);

        if (ShowInput)
        {
            if (suggestions.Count > 0)
            {
                items.Add(
                    CommandSuggestionSection.Render(
                        suggestions,
                        selectedSuggestionIndex,
                        contentWidth
                    )
                );
            }

            items.Add(BuildInputRenderable(contentWidth));
            items.Add(new Text(string.Empty));
        }

        items.Add(
            FooterSection.Render(
                state.Phase,
                state.AwaitingUserToken,
                state.ShowHelp,
                ShouldShowScrollActions(contentHeight)
            )
        );
        return items;
    }

    private IRenderable BuildInputRenderable(int contentWidth)
    {
        var state = Controller.State;
        var inputBuffer = Controller.InputBuffer;

        return state.AwaitingUserToken
            ? InputSection.Render(
                MaskTokenInput(inputBuffer.Length),
                state.CursorVisible,
                contentWidth,
                prompt: "token>",
                placeholder: "Paste Apple Music user token (input hidden)"
            )
            : InputSection.Render(inputBuffer.ToString(), state.CursorVisible, contentWidth);
    }

    private static string MaskTokenInput(int length) =>
        length > 0 ? $"Token hidden ({length} {PluralizeCharacter(length)})" : string.Empty;

    private static string PluralizeCharacter(int count) => count == 1 ? "char" : "chars";

    private Rows BuildStatusSection()
    {
        var state = Controller.State;
        var badges = StatusSection.RenderBadges(
            tokenCache.HasValidDeveloperToken,
            tokenCache.HasValidUserToken,
            state.Storefront,
            state.NextPlaylistName
        );

        return new Rows(
            badges,
            new Text(string.Empty),
            StatusSection.RenderStepper(state.Phase),
            new Text(string.Empty)
        );
    }

    private IRenderable BuildMainContent(int contentWidth, int contentHeight) =>
        Controller.State.Phase switch
        {
            TuiTransferPhase.Done => RenderDoneSection(contentWidth, contentHeight),
            _ => RenderActiveSection(contentWidth, contentHeight),
        };

    private IRenderable RenderActiveSection(int width, int contentHeight)
    {
        var state = Controller.State;
        var progressLines = 0;
        IRenderable? progressSection = null;

        switch (state.Phase)
        {
            case TuiTransferPhase.ConfirmPlaylist:
                progressLines = PlaylistConfirmationHeight;
                progressSection = TransferProgressSection.RenderPlaylistConfirmation(
                    state.PlaylistName,
                    state.TransferTracks.Count,
                    state.Storefront
                );
                break;
            case TuiTransferPhase.FetchingPlaylist:
                progressLines = SpinnerSectionHeight;
                progressSection = TransferProgressSection.RenderSpinnerLine(
                    "Fetching Spotify playlist...",
                    state.SpinnerTick
                );
                break;
            case TuiTransferPhase.ResolvingIsrc
                when state.ProgressTotal > 0 && state.ProgressCurrent >= state.ProgressTotal:
                progressLines = SpinnerSectionHeight;
                progressSection = TransferProgressSection.RenderSpinnerLine(
                    "Matching against Apple Music...",
                    state.SpinnerTick
                );
                break;
            case TuiTransferPhase.ResolvingIsrc:
                progressLines = ProgressSectionHeight;
                progressSection = TransferProgressSection.RenderProgressBar(
                    $"Resolving ISRCs via Deezer ({state.ProgressCurrent}/{state.ProgressTotal})",
                    state.ProgressTotal > 0
                        ? (double)state.ProgressCurrent / state.ProgressTotal
                        : 0,
                    width
                );
                break;
            case TuiTransferPhase.ConfirmTextMatch:
                progressLines = SpinnerSectionHeight;
                progressSection = TransferProgressSection.RenderConfirmPrompt(
                    state.UnmatchedTracks.Count
                );
                break;
            case TuiTransferPhase.TextMatching:
                progressLines = ProgressSectionHeight;
                progressSection = TransferProgressSection.RenderProgressBar(
                    string.IsNullOrEmpty(state.ProgressLabel)
                        ? "Text matching..."
                        : $"Matching: {state.ProgressLabel}",
                    state.ProgressTotal > 0
                        ? (double)state.ProgressCurrent / state.ProgressTotal
                        : 0,
                    width
                );
                break;
            case TuiTransferPhase.CreatingPlaylist:
                progressLines = SpinnerSectionHeight;
                progressSection = TransferProgressSection.RenderSpinnerLine(
                    "Creating Apple Music playlist...",
                    state.SpinnerTick
                );
                break;
        }

        var logHeight =
            progressLines > 0
                ? Math.Max(MinLogHeight, contentHeight - progressLines - QueuePanelGapHeight)
                : contentHeight;

        if (state is { ShowHelp: true, Phase: TuiTransferPhase.Idle })
            return BuildHelpSection(contentHeight);

        if (state.QueuedPlaylistUrls.Count > 0 && state.Phase is TuiTransferPhase.Idle)
        {
            return BuildQueuedPlaylistsView(width, contentHeight);
        }

        var logs = LogSection.Render(Controller.Logs, width, logHeight);

        if (progressSection is not null)
            return new Rows(logs, new Text(string.Empty), progressSection);

        return logs;
    }

    private Align BuildHelpSection(int contentHeight)
    {
        var state = Controller.State;
        var visibleRows = GetVisibleHelpRows(contentHeight);
        var table = HelpSection.RenderTable(state.ScrollOffset, visibleRows);
        var hint = HelpSection.RenderScrollHint(
            state.ScrollOffset,
            HelpSection.EntryCount,
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
        var state = Controller.State;
        var queuePanelHeight = QueuePanelChromeHeight + GetVisibleQueueItemsCount();
        var queueLogHeight = Math.Max(
            MinQueueLogHeight,
            contentHeight - queuePanelHeight - QueuePanelGapHeight
        );
        var logArea = LogSection.Render(Controller.Logs, width, queueLogHeight);

        return new Rows(
            logArea,
            new Text(string.Empty),
            QueueSection.Render(state.QueuedPlaylistUrls)
        );
    }

    private int GetVisibleQueueItemsCount()
    {
        var state = Controller.State;
        var visibleQueueItems = Math.Min(
            state.QueuedPlaylistUrls.Count,
            QueueSection.MaxVisibleItems
        );
        var hasExtraItems = state.QueuedPlaylistUrls.Count > QueueSection.MaxVisibleItems ? 1 : 0;
        return visibleQueueItems + hasExtraItems;
    }

    private IRenderable RenderDoneSection(int width, int contentHeight)
    {
        var state = Controller.State;

        if (state.TransferTracks.Count == 0)
            return new Text("No results");

        var matched = state.AllResults.OfType<MatchResult.Matched>().Count();
        var total = state.TransferTracks.Count;
        var notFound = total - matched;

        var summary = ResultsSection.RenderSummaryPanel(
            state.PlaylistName,
            matched,
            total,
            notFound
        );
        var visibleRows = GetVisibleDoneRows(contentHeight);
        var table = ResultsSection.RenderTable(
            state.AllResults,
            state.ScrollOffset,
            visibleRows,
            width
        );
        var hint = ResultsSection.RenderScrollHint(
            state.ScrollOffset,
            state.AllResults.Count,
            visibleRows
        );

        return new Rows(summary, new Text(string.Empty), table, new Text(string.Empty), hint);
    }

    private bool ShouldShowStatusSection(int contentWidth) =>
        !Controller.State.ShowHelp && contentWidth >= MinStatusSectionWidth;

    private bool ShouldShowScrollActions(int contentHeight)
    {
        var state = Controller.State;

        if (state is { ShowHelp: true, Phase: TuiTransferPhase.Idle })
            return HelpSection.EntryCount > GetVisibleHelpRows(contentHeight);

        if (state.Phase is not TuiTransferPhase.Done)
        {
            return false;
        }

        return state.AllResults.Count > GetVisibleDoneRows(contentHeight);
    }

    private static int GetVisibleRows(int contentHeight, int chromeHeight) =>
        Math.Max(3, contentHeight - chromeHeight);

    private static int GetVisibleDoneRows(int contentHeight) =>
        GetVisibleRows(contentHeight, DoneViewChromeHeight);

    private int GetVisibleDoneRows() =>
        GetVisibleDoneRows(Console.WindowHeight - CurrentFixedChromeHeight);

    private static int GetVisibleHelpRows(int contentHeight) =>
        GetVisibleRows(contentHeight, HelpSectionChromeHeight);

    private int GetVisibleHelpRows() =>
        GetVisibleHelpRows(Console.WindowHeight - CurrentFixedChromeHeight);
}
