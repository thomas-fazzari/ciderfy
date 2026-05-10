using Spectre.Console;

namespace Ciderfy.Tui;

internal static class FooterSection
{
    internal static Markup Render(
        TuiTransferPhase phase,
        bool awaitingUserToken,
        bool showHelp,
        bool showScrollActions
    )
    {
        if (showHelp)
        {
            if (showScrollActions)
            {
                return new Markup(
                    $"[{Theme.Primary}]Up/Down[/] scroll  [{Theme.Primary}]Enter[/] hide help  [{Theme.Primary}]Ctrl+C[/] quit"
                );
            }

            return new Markup(
                $"[{Theme.Primary}]Enter[/] hide help  [{Theme.Primary}]Ctrl+C[/] quit"
            );
        }

        if (awaitingUserToken)
        {
            return new Markup(
                $"[{Theme.Primary}]Enter[/] submit token  [{Theme.Muted}]Esc[/] cancel  [{Theme.Muted}]Ctrl+C[/] quit"
            );
        }

        if (phase is TuiTransferPhase.Done)
        {
            if (showScrollActions)
            {
                return new Markup(
                    $"[{Theme.Primary}]Up/Down[/] scroll  [{Theme.Primary}]Enter[/] new transfer  [{Theme.Muted}]Ctrl+C[/] quit"
                );
            }

            return new Markup(
                $"[{Theme.Primary}]Enter[/] new transfer  [{Theme.Muted}]Ctrl+C[/] quit"
            );
        }

        return phase switch
        {
            TuiTransferPhase.Idle => new Markup(
                $"[{Theme.Primary}]Enter[/] submit  [{Theme.Muted}]/help[/] commands  [{Theme.Muted}]Ctrl+C[/] quit"
            ),
            TuiTransferPhase.ConfirmPlaylist => new Markup(
                $"[{Theme.Primary}]Enter/Y[/] proceed  [{Theme.Muted}]Esc/N[/] cancel  [{Theme.Muted}]Ctrl+C[/] quit"
            ),
            TuiTransferPhase.ConfirmTextMatch => new Markup(
                $"[{Theme.Primary}]Enter/Y[/] match text  [{Theme.Muted}]N[/] skip  [{Theme.Muted}]Ctrl+C[/] quit"
            ),
            _ => new Markup($"[{Theme.Muted}]Working...[/]  [{Theme.Muted}]Ctrl+C[/] quit"),
        };
    }
}
