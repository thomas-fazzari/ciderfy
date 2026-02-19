using Ciderfy.Apple;
using Ciderfy.Configuration.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ciderfy.Tests;

[IntegrationTest]
public class AppleMusicConnectionTests
{
    private readonly TokenCache _tokenCache;

    public AppleMusicConnectionTests()
    {
        _tokenCache = TokenCache.Load();
        if (!_tokenCache.HasValidDeveloperToken)
            Assert.Skip("Ignored: no token available in cache");
    }

    [Fact]
    public async Task ExtractDeveloperToken_ReturnsValidJwt()
    {
        // Arrange
        using var authClient = TestHttpClients.CreateAppleMusicAuthClient();
        var auth = new AppleMusicAuth(_tokenCache, authClient);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var token = await auth.GetDeveloperTokenAsync(ct);

        // Assert
        Assert.NotNull(token);
        Assert.StartsWith("eyJ", token);
        Assert.Equal(3, token.Split('.').Length); // JWT has 3 parts
    }

    [Fact]
    public async Task ExtractDeveloperToken_IsCached()
    {
        // Arrange
        using var authClient = TestHttpClients.CreateAppleMusicAuthClient();
        var auth = new AppleMusicAuth(_tokenCache, authClient);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var token1 = await auth.GetDeveloperTokenAsync(ct);
        var token2 = await auth.GetDeveloperTokenAsync(ct);

        // Assert
        Assert.Equal(token1, token2);
        Assert.True(_tokenCache.HasValidDeveloperToken);
    }

    [Fact]
    public async Task SearchCatalogByText_ReturnsResults()
    {
        // Arrange
        using var authClient = TestHttpClients.CreateAppleMusicAuthClient();
        var auth = new AppleMusicAuth(_tokenCache, authClient);
        using var appleMusicHttpClient = TestHttpClients.CreateAppleMusicClient();
        using var client = new AppleMusicClient(
            appleMusicHttpClient,
            Options.Create(new AppleMusicClientOptions()),
            _tokenCache
        );
        var ct = TestContext.Current.CancellationToken;

        await auth.GetDeveloperTokenAsync(ct);

        // Act
        var results = await client.SearchByTextAllAsync("Revolution 909 Daft Punk", ct: ct);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(
            results,
            r => r.Artist.Contains("Daft Punk", StringComparison.OrdinalIgnoreCase)
        );
    }
}
