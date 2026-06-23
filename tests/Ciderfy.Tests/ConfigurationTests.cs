using System.Diagnostics;
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

            configuration.AddCiderfyConfiguration(
                tempDir,
                Path.Combine(AppContext.BaseDirectory, "ciderfy.ini")
            );

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

    [Fact]
    public void ConfigurationFolderOpener_ConfigDirectory_UsesAppPathsConfigDirectory()
    {
        var opener = new ConfigurationFolderOpener();

        Assert.Equal(AppPaths.ConfigDirectory, opener.ConfigDirectory);
    }

    [Fact]
    public void ConfigurationFolderOpener_Open_CreatesDirectoryAndStartsNativeExplorer()
    {
        var rootDir = CreateTempDirectory();
        var configDir = Path.Combine(rootDir, "config");
        try
        {
            ProcessStartInfo? startInfo = null;
            var opener = new ConfigurationFolderOpener(
                configDir,
                info =>
                {
                    startInfo = info;
                    return new Process();
                }
            );

            opener.Open();

            Assert.True(Directory.Exists(opener.ConfigDirectory));
            Assert.NotNull(startInfo);
            Assert.Equal(GetExpectedExplorerFileName(), startInfo.FileName);
            Assert.False(startInfo.UseShellExecute);
            Assert.Equal([opener.ConfigDirectory], startInfo.ArgumentList);
        }
        finally
        {
            Directory.Delete(rootDir, recursive: true);
        }
    }

    [Fact]
    public void ConfigurationFolderOpener_Open_ThrowsWhenNativeExplorerCannotStart()
    {
        var rootDir = CreateTempDirectory();
        var configDir = Path.Combine(rootDir, "config");
        try
        {
            var opener = new ConfigurationFolderOpener(configDir, _ => null);

            var exception = Assert.Throws<InvalidOperationException>(opener.Open);

            Assert.Equal("Could not start the native file explorer.", exception.Message);
            Assert.True(Directory.Exists(configDir));
        }
        finally
        {
            Directory.Delete(rootDir, recursive: true);
        }
    }

    [Theory]
    [InlineData(typeof(IOException), true)]
    [InlineData(typeof(UnauthorizedAccessException), true)]
    [InlineData(typeof(InvalidOperationException), true)]
    [InlineData(typeof(PlatformNotSupportedException), true)]
    [InlineData(typeof(System.ComponentModel.Win32Exception), true)]
    [InlineData(typeof(ArgumentException), false)]
    public void ConfigurationFolderOpener_IsOpenFailure_ClassifiesExpectedExceptions(
        Type exceptionType,
        bool expected
    )
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        var opener = new ConfigurationFolderOpener();

        var isOpenFailure = opener.IsOpenFailure(exception);

        Assert.Equal(expected, isOpenFailure);
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
        {
            return "open";
        }

        if (OperatingSystem.IsWindows())
        {
            return "explorer.exe";
        }

        if (OperatingSystem.IsLinux())
        {
            return "xdg-open";
        }

        throw new PlatformNotSupportedException();
    }
}
