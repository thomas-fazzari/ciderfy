using System.Threading.Channels;

namespace Ciderfy.Tui;

internal sealed class TuiTransferSession : IDisposable
{
    private CancellationTokenSource? _currentCts;

    internal int Id { get; private set; }

    internal int Begin()
    {
        CancelCurrent();
        return ++Id;
    }

    internal void CancelAndInvalidate()
    {
        CancelCurrent();
        Id++;
    }

    internal void CancelCurrent()
    {
        Interlocked.Exchange(ref _currentCts, null)?.Cancel();
    }

    internal bool IsStale(int transferId) => transferId != Id;

    internal void Start(
        int transferId,
        Func<int, CancellationToken, Task> operation,
        ChannelWriter<TuiMessage> messages,
        CancellationToken appToken
    )
    {
        var operationCts = CancellationTokenSource.CreateLinkedTokenSource(appToken);
        _currentCts = operationCts;

        var task = Task.Run(
            () => RunAsync(transferId, operation, operationCts, messages, appToken),
            CancellationToken.None
        );
        _ = task.ContinueWith(
            t => messages.TryWrite(new FatalErrorMsg(t.Exception!.GetBaseException())),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default
        );
    }

    private async Task RunAsync(
        int transferId,
        Func<int, CancellationToken, Task> operation,
        CancellationTokenSource operationCts,
        ChannelWriter<TuiMessage> messages,
        CancellationToken appToken
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
            Interlocked.CompareExchange(ref _currentCts, null, operationCts);
            operationCts.Dispose();
        }
    }

    public void Dispose()
    {
        _currentCts?.Cancel();
        _currentCts?.Dispose();
        _currentCts = null;
    }
}
