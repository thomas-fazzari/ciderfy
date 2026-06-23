using System.Threading.Channels;
using Ciderfy.Apple;
using Ciderfy.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Ciderfy.Tui;

/// <summary>
/// Full-screen alternate-screen TUI that drives the Spotify to Apple Music transfer pipeline
/// </summary>
internal sealed partial class TuiApp(
    TokenCache tokenCache,
    IServiceScopeFactory scopeFactory,
    ConfigurationFolderOpener configurationFolderOpener
) : IDisposable
{
    private readonly Channel<TuiMessage> _channel = Channel.CreateUnbounded<TuiMessage>();
    private readonly CancellationTokenSource _cts = new();
    private TuiEffectRunner? _effectRunner;

    private TuiEffectRunner EffectRunner =>
        _effectRunner ??= new TuiEffectRunner(
            tokenCache,
            scopeFactory,
            configurationFolderOpener,
            _channel.Writer,
            () => _cts.Cancel(),
            _cts.Token
        );

    private TuiController Controller =>
        field ??= new TuiController(tokenCache, GetVisibleHelpRows, GetVisibleDoneRows);

    /// <summary>
    /// Enters the alternate screen and runs the TUI event loop until the user quits
    /// </summary>
    public Task<int> RunAsync()
    {
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
            EffectRunner.ExecuteAll(Controller.ProcessMessage(msg));
        }
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

    public void Dispose()
    {
        _effectRunner?.Dispose();
        _cts.Cancel();
        _channel.Writer.TryComplete();
        _cts.Dispose();
    }
}
