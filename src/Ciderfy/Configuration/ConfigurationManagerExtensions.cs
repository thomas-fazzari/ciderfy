using Microsoft.Extensions.Configuration;

namespace Ciderfy.Configuration;

internal static class ConfigurationManagerExtensions
{
    private const string ConfigFileName = "ciderfy.ini";

    extension(ConfigurationManager configuration)
    {
        internal ConfigurationManager AddCiderfyConfiguration() =>
            configuration.AddCiderfyConfiguration(AppPaths.ConfigDirectory, GetTemplatePath());

        internal ConfigurationManager AddCiderfyConfiguration(
            string configDirectory,
            string templatePath
        )
        {
            configuration.Sources.Clear();
            Directory.CreateDirectory(configDirectory);
            EnsureUserConfigExists(configDirectory, templatePath);
            configuration.SetBasePath(configDirectory);
            configuration.AddIniFile(ConfigFileName, optional: false, reloadOnChange: false);

            return configuration;
        }
    }

    private static string GetTemplatePath() =>
        Path.Combine(AppContext.BaseDirectory, ConfigFileName);

    private static void EnsureUserConfigExists(string configDirectory, string templatePath)
    {
        var configPath = Path.Combine(configDirectory, ConfigFileName);
        if (File.Exists(configPath))
            return;

        File.Copy(templatePath, configPath);
    }
}
