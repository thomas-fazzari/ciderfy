using Spectre.Console;
using Spectre.Console.Rendering;

namespace Ciderfy.Tui;

internal static class StatusSection
{
    private static readonly string[] _stepLabels = ["Setup", "Fetch", "Match", "Transfer", "Done"];

    internal static Align RenderBadges(
        bool hasValidDeveloperToken,
        bool hasValidUserToken,
        string storefront,
        string? nextPlaylistName
    )
    {
        var devBadge = hasValidDeveloperToken
            ? RenderHelpers.Badge("Developer valid", Theme.BadgeFg, Theme.BadgeGoodBg)
            : RenderHelpers.Badge("Developer missing", Theme.BadgeFg, Theme.BadgeBadBg);

        var userBadge = hasValidUserToken
            ? RenderHelpers.Badge("User valid", Theme.BadgeFg, Theme.BadgeGoodBg)
            : RenderHelpers.Badge("User missing", Theme.BadgeFg, Theme.BadgeBadBg);

        var sfBadge = RenderHelpers.Badge(
            $"Storefront {storefront.ToUpperInvariant()}",
            Theme.BadgeFg,
            Theme.BadgeNeutralBg
        );
        var nameBadge = string.IsNullOrWhiteSpace(nextPlaylistName)
            ? RenderHelpers.Badge("Name auto", Theme.BadgeFg, Theme.BadgeNeutralBg)
            : RenderHelpers.Badge("Name custom", Theme.BadgeFg, Theme.BadgeNeutralBg);

        return Align.Center(new Markup($"{devBadge} {userBadge} {sfBadge} {nameBadge}"));
    }

    internal static IRenderable RenderStepper(TuiTransferPhase phase)
    {
        var currentStep = phase switch
        {
            TuiTransferPhase.FetchingPlaylist => 1,
            TuiTransferPhase.ResolvingIsrc
            or TuiTransferPhase.ConfirmTextMatch
            or TuiTransferPhase.TextMatching => 2,
            TuiTransferPhase.CreatingPlaylist => 3,
            TuiTransferPhase.Done => 4,
            _ => 0,
        };

        var parts = _stepLabels.Select(
            (step, index) =>
            {
                var escapedStep = Markup.Escape(step);
                if (index < currentStep)
                {
                    return $"[{Theme.Teal}]{escapedStep}[/]";
                }

                if (index == currentStep)
                {
                    return $"[{Theme.Primary} bold]{escapedStep}[/]";
                }

                return $"[{Theme.Muted}]{escapedStep}[/]";
            }
        );

        return Align.Center(
            new Markup(string.Join($" [{Theme.Gray}]{Theme.ChevronRight}[/] ", parts))
        );
    }
}
