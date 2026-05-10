using Ciderfy.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ciderfy.Tests;

public class ConfigurationTests
{
    [Fact]
    public void AddCiderfyConfiguration_LoadsIniFromConfigDirectory()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "ciderfy.ini"),
                """
                [Spotify]
                PlaylistQueryHash=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                TotpVersion=42
                """
            );
            var configuration = new ConfigurationManager();
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Spotify:TotpVersion"] = "99" }
            );

            configuration.AddCiderfyConfiguration(tempDir);

            Assert.Equal(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                configuration["Spotify:PlaylistQueryHash"]
            );
            Assert.Equal("42", configuration["Spotify:TotpVersion"]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void AddCiderfyConfiguration_CreatesMissingConfigDirectory()
    {
        var rootDir = CreateTempDirectory();
        var tempDir = Path.Combine(rootDir, "config");
        var templatePath = Path.Combine(rootDir, "template.ini");
        try
        {
            File.WriteAllText(templatePath, "[Spotify]" + Environment.NewLine + "TotpVersion=42");
            var configuration = new ConfigurationManager();

            configuration.AddCiderfyConfiguration(tempDir, templatePath);

            Assert.True(Directory.Exists(tempDir));
        }
        finally
        {
            Directory.Delete(rootDir, recursive: true);
        }
    }

    [Fact]
    public void AddCiderfyConfiguration_CopiesTemplateWhenMissing()
    {
        var rootDir = CreateTempDirectory();
        var configDir = Path.Combine(rootDir, "config");
        var templatePath = Path.Combine(rootDir, "template.ini");
        const string template = "[Spotify]\nTotpVersion=42";
        try
        {
            File.WriteAllText(templatePath, template);
            var configuration = new ConfigurationManager();

            configuration.AddCiderfyConfiguration(configDir, templatePath);

            var configPath = Path.Combine(configDir, "ciderfy.ini");
            Assert.True(File.Exists(configPath));
            Assert.Equal(template, File.ReadAllText(configPath));
            Assert.Equal("42", configuration["Spotify:TotpVersion"]);
        }
        finally
        {
            Directory.Delete(rootDir, recursive: true);
        }
    }

    [Fact]
    public void ConfigurationFolderOpener_CreateStartInfo_UsesNativeExplorerForCurrentOs()
    {
        const string directory = "/tmp/ciderfy-config";

        var startInfo = ConfigurationFolderOpener.CreateStartInfo(directory);

        var expectedFileName = GetExpectedExplorerFileName();
        Assert.Equal(expectedFileName, startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal([directory], startInfo.ArgumentList);
    }

    private static string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ciderfy-config-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static string GetExpectedExplorerFileName()
    {
        if (OperatingSystem.IsMacOS())
            return "open";

        if (OperatingSystem.IsWindows())
            return "explorer.exe";

        if (OperatingSystem.IsLinux())
            return "xdg-open";

        throw new PlatformNotSupportedException();
    }
}
