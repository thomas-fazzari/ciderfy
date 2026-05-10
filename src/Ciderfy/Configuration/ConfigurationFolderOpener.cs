using System.ComponentModel;
using System.Diagnostics;

namespace Ciderfy.Configuration;

internal static class ConfigurationFolderOpener
{
    internal static string ConfigDirectory => AppPaths.ConfigDirectory;

    internal static void Open()
    {
        Directory.CreateDirectory(ConfigDirectory);

        using var process =
            Process.Start(CreateStartInfo(ConfigDirectory))
            ?? throw new InvalidOperationException("Could not start the native file explorer.");
    }

    internal static ProcessStartInfo CreateStartInfo(string directory)
    {
        if (OperatingSystem.IsMacOS())
            return CreateStartInfo("open", directory);

        if (OperatingSystem.IsWindows())
            return CreateStartInfo("explorer.exe", directory);

        if (OperatingSystem.IsLinux())
            return CreateStartInfo("xdg-open", directory);

        throw new PlatformNotSupportedException("Opening a folder is not supported on this OS.");
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, string directory)
    {
        var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = false };
        startInfo.ArgumentList.Add(directory);

        return startInfo;
    }

    internal static bool IsOpenFailure(Exception exception) =>
        exception
            is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or PlatformNotSupportedException
                or Win32Exception;
}
