namespace Ciderfy.Configuration;

internal static class AppPaths
{
    internal static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Ciderfy"
    );

    internal static string TokenCachePath => Path.Combine(ConfigDirectory, "tokens.json");
}
