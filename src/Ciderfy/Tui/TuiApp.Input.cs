namespace Ciderfy.Tui;

internal sealed partial class TuiApp
{
    private void ReadKeysLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!TryReadKey(out var key))
            {
                continue;
            }

            _channel.Writer.TryWrite(new KeyPressedMsg(key));
        }
    }

    private static bool TryReadKey(out ConsoleKeyInfo key)
    {
        if (!Console.KeyAvailable)
        {
            Thread.Sleep(20);
            key = default;
            return false;
        }

        key = Console.ReadKey(true);
        return true;
    }
}
