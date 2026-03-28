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
        var cache = new TokenCache { DeveloperToken = "" };
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
        var cache = new TokenCache { UserToken = "" };
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
}
