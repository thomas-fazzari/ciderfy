using Ciderfy.Matching;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Ciderfy.Tui;

internal sealed partial class TuiApp
{
    // Lines consumed by fixed UI elements (everything except the content area).
    // Panel border (2) + Banner(6) + Rule (1) + Badges (1) + Gaps(2) + Input panel (3) + Footer (1)
    private const int FixedChromeHeight = 2 + 6 + 1 + 1 + 2 + 3 + 1;

    // Lines consumed within the Done view by non-table elements.
    // Summary panel (3) + Gap(1) + Table header and borders (3) + Gap (1) + Hint (1)
    private const int DoneViewChromeHeight = 3 + 1 + 3 + 1 + 1;

    private Panel BuildView()
    {
        var width = Math.Max(24, Console.WindowWidth);
        var height = Console.WindowHeight;
        var contentWidth = Math.Max(20, width - 4);
        var contentHeight = Math.Max(4, height - FixedChromeHeight);

        // Banner + separator + badges
        var banner = Components.RenderBanner(contentWidth);
        var separator = new Rule { Style = new Style(Theme.GrayColor) };
        var badges = Components.RenderStatusBadges(
            tokenCache.HasValidDeveloperToken,
            tokenCache.HasValidUserToken,
            _storefront,
            _nextPlaylistName
        );

        // Main content area
        var content = _phase switch
        {
            TuiTransferPhase.Done => RenderDoneView(contentWidth, contentHeight),
            TuiTransferPhase.ConfirmPlaylist => Components.RenderPlaylistConfirmation(
                _playlistName,
                _transferTracks?.Count ?? 0,
                _storefront
            ),
            _ => RenderActiveView(contentWidth, contentHeight),
        };

        // Input
        IRenderable input;
        if (_awaitingUserToken)
            input = Components.RenderInputWithPrompt(
                "token>",
                _inputBuffer.ToString(),
                _cursorVisible,
                contentWidth
            );
        else
            input = Components.RenderInput(_inputBuffer.ToString(), _cursorVisible, contentWidth);

        var footer = Components.RenderFooter();

        var rows = new Rows(
            banner,
            separator,
            badges,
            new Text(""),
            content,
            new Text(""),
            input,
            footer
        );

        return new Panel(rows)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Theme.GrayColor),
            Padding = new Padding(1, 0),
            Width = width,
            Height = height,
        };
    }

    private IRenderable RenderActiveView(int width, int contentHeight)
    {
        var (progressLines, progressSection) = _phase switch
        {
            TuiTransferPhase.FetchingPlaylist => (
                1,
                Components.RenderSpinnerLine("Fetching Spotify playlist...", _spinnerTick)
            ),
            TuiTransferPhase.ResolvingIsrc => (
                2,
                Components.RenderProgressBar(
                    $"Resolving ISRCs via Deezer ({_progressCurrent}/{_progressTotal})",
                    _progressTotal > 0 ? (double)_progressCurrent / _progressTotal : 0,
                    width
                )
            ),
            TuiTransferPhase.ConfirmTextMatch => (
                1,
                Components.RenderConfirmPrompt(_unmatchedTracks?.Count ?? 0)
            ),
            TuiTransferPhase.TextMatching => (
                2,
                Components.RenderProgressBar(
                    string.IsNullOrEmpty(_progressLabel)
                        ? "Text matching..."
                        : $"Matching: {_progressLabel}",
                    _progressTotal > 0 ? (double)_progressCurrent / _progressTotal : 0,
                    width
                )
            ),
            TuiTransferPhase.CreatingPlaylist => (
                1,
                Components.RenderSpinnerLine("Creating Apple Music playlist...", _spinnerTick)
            ),
            _ => (0, null),
        };

        var logHeight =
            progressLines > 0 ? Math.Max(4, contentHeight - progressLines - 1) : contentHeight;

        if (_showHelp && _phase is TuiTransferPhase.Idle)
        {
            var helpHeight = 12;
            var helpLogHeight = Math.Max(2, contentHeight - helpHeight - 2);
            var logArea = Components.RenderLogArea(_logs, width, helpLogHeight);
            return new Rows(
                logArea,
                new Text(""),
                Components.RenderHelpTable(),
                new Markup($"[{Theme.Muted}]Press Enter to dismiss[/]")
            );
        }

        var logs = Components.RenderLogArea(_logs, width, logHeight);

        if (progressSection is not null)
            return new Rows(logs, new Text(""), progressSection);

        return logs;
    }

    private IRenderable RenderDoneView(int width, int contentHeight)
    {
        if (_allResults is null || _transferTracks is null)
            return new Text("No results");

        var matched = _allResults.OfType<MatchResult.Matched>().Count();
        var total = _transferTracks.Count;
        var notFound = total - matched;

        var summary = Components.RenderSummaryPanel(_playlistName, matched, total, notFound);
        var visibleRows = Math.Max(3, contentHeight - DoneViewChromeHeight);
        var table = Components.RenderResultsTable(_allResults, _scrollOffset, visibleRows, width);
        var hint = Components.RenderScrollHint(_scrollOffset, _allResults.Count, visibleRows);

        return new Rows(summary, new Text(""), table, new Text(""), hint);
    }

    private string StatusSummary()
    {
        var dev = tokenCache.HasValidDeveloperToken ? "valid" : "missing";
        var user = tokenCache.HasValidUserToken ? "valid" : "missing";
        return $"Developer token: {dev}  |  User token: {user}  |  Storefront: {_storefront}";
    }
}
