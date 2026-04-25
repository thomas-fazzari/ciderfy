using Ciderfy.Apple;
using Xunit;

namespace Ciderfy.Tests;

public class TokenCacheTests
{
    [Fact]
    public void HasValidDeveloperToken_NullToken_ReturnsFalse()
    {
        var cache = new TokenCache();
        Assert.False(cache.HasValidDeveloperToken);
    }

    [Fact]
    public void HasValidDeveloperToken_EmptyToken_ReturnsFalse()
    {
        var cache = new TokenCache { DeveloperToken = string.Empty };
        Assert.False(cache.HasValidDeveloperToken);
    }

    [Fact]
    public void HasValidDeveloperToken_NoExpiry_ReturnsFalse()
    {
        var cache = new TokenCache { DeveloperToken = "token", DeveloperTokenExpiry = null };
        Assert.False(cache.HasValidDeveloperToken);
    }

    [Fact]
    public void HasValidDeveloperToken_ExpiredToken_ReturnsFalse()
    {
        var cache = new TokenCache
        {
            DeveloperToken = "token",
            DeveloperTokenExpiry = DateTimeOffset.UtcNow.AddSeconds(-1),
        };
        Assert.False(cache.HasValidDeveloperToken);
    }

    [Fact]
    public void HasValidDeveloperToken_ValidToken_ReturnsTrue()
    {
        var cache = new TokenCache
        {
            DeveloperToken = "token",
            DeveloperTokenExpiry = DateTimeOffset.UtcNow.AddHours(1),
        };
        Assert.True(cache.HasValidDeveloperToken);
    }

    [Fact]
    public void HasValidUserToken_NullToken_ReturnsFalse()
    {
        var cache = new TokenCache();
        Assert.False(cache.HasValidUserToken);
    }

    [Fact]
    public void HasValidUserToken_EmptyToken_ReturnsFalse()
    {
        var cache = new TokenCache { UserToken = string.Empty };
        Assert.False(cache.HasValidUserToken);
    }

    [Fact]
    public void HasValidUserToken_NoExpiry_ReturnsFalse()
    {
        var cache = new TokenCache { UserToken = "token", UserTokenExpiry = null };
        Assert.False(cache.HasValidUserToken);
    }

    [Fact]
    public void HasValidUserToken_ExpiredToken_ReturnsFalse()
    {
        var cache = new TokenCache
        {
            UserToken = "token",
            UserTokenExpiry = DateTimeOffset.UtcNow.AddSeconds(-1),
        };
        Assert.False(cache.HasValidUserToken);
    }

    [Fact]
    public void HasValidUserToken_ValidToken_ReturnsTrue()
    {
        var cache = new TokenCache
        {
            UserToken = "token",
            UserTokenExpiry = DateTimeOffset.UtcNow.AddHours(1),
        };
        Assert.True(cache.HasValidUserToken);
    }

    [Fact]
    public void Clear_ResetsAllProperties()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var cache = new TokenCache(Path.Combine(tempDir, "tokens.json"))
            {
                DeveloperToken = "dev",
                DeveloperTokenExpiry = DateTimeOffset.UtcNow.AddHours(1),
                UserToken = "user",
                UserTokenExpiry = DateTimeOffset.UtcNow.AddHours(1),
            };

            cache.Clear();

            Assert.Null(cache.DeveloperToken);
            Assert.Null(cache.DeveloperTokenExpiry);
            Assert.Null(cache.UserToken);
            Assert.Null(cache.UserTokenExpiry);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ClearDeveloperToken_ClearsDeveloperToken_PreservesUserToken()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var expiry = DateTimeOffset.UtcNow.AddHours(1);
            var cache = new TokenCache(Path.Combine(tempDir, "tokens.json"))
            {
                DeveloperToken = "dev",
                DeveloperTokenExpiry = expiry,
                UserToken = "user",
                UserTokenExpiry = expiry,
            };

            cache.ClearDeveloperToken();

            Assert.Null(cache.DeveloperToken);
            Assert.Null(cache.DeveloperTokenExpiry);
            Assert.Equal("user", cache.UserToken);
            Assert.Equal(expiry, cache.UserTokenExpiry);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadFromPath_CorruptedJson_ReturnsEmptyCache()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var cachePath = Path.Combine(tempDir, "tokens.json");
            File.WriteAllText(cachePath, "{not-json");

            var cache = TokenCache.LoadFromPath(cachePath);

            Assert.Null(cache.DeveloperToken);
            Assert.Null(cache.DeveloperTokenExpiry);
            Assert.Null(cache.UserToken);
            Assert.Null(cache.UserTokenExpiry);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_ThenLoadFromPath_RoundTripsData()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var cachePath = Path.Combine(tempDir, "tokens.json");
            var expiry = DateTimeOffset.UtcNow.AddHours(1);
            var cache = new TokenCache(cachePath)
            {
                DeveloperToken = "dev-token",
                DeveloperTokenExpiry = expiry,
                UserToken = "user-token",
                UserTokenExpiry = null,
            };

            cache.Save();

            var loaded = TokenCache.LoadFromPath(cachePath);

            Assert.Equal("dev-token", loaded.DeveloperToken);
            Assert.Equal(expiry, loaded.DeveloperTokenExpiry);
            Assert.Equal("user-token", loaded.UserToken);
            Assert.Null(loaded.UserTokenExpiry);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_WhenReplaceFails_DoesNotThrow_AndCleansTempFile()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var destinationDirectory = Path.Combine(tempDir, "tokens");
            Directory.CreateDirectory(destinationDirectory);

            var cache = new TokenCache(destinationDirectory)
            {
                DeveloperToken = "dev-token",
                DeveloperTokenExpiry = DateTimeOffset.UtcNow.AddHours(1),
            };

            var error = Record.Exception(() => cache.Save());

            Assert.Null(error);
            Assert.False(File.Exists($"{destinationDirectory}.tmp"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(),
            $"ciderfy-token-cache-tests-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }
}
