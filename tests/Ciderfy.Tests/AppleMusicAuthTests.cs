using Ciderfy.Apple;
using Ciderfy.Tests.Fakers;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ciderfy.Tests;

public class AppleMusicAuthTests
{
    private const string ValidAlg = "ES256";
    private const string TestKid = "ABCD1234";
    private const string TestIssuer = "TEAMID";
    private const string BaseUrl = "https://music.test";

    // IsAppleMusicJwt
    [Fact]
    public void IsAppleMusicJwt_ValidToken_ReturnsTrue()
    {
        var jwt = JwtFaker.Make(
            new { alg = ValidAlg, kid = TestKid },
            new { iss = TestIssuer, exp = JwtFaker.FutureExp }
        );

        Assert.True(AppleMusicAuth.IsAppleMusicJwt(jwt));
    }

    [Fact]
    public void IsAppleMusicJwt_WrongAlgorithm_ReturnsFalse()
    {
        var jwt = JwtFaker.Make(
            new { alg = "RS256", kid = TestKid },
            new { iss = TestIssuer, exp = JwtFaker.FutureExp }
        );

        Assert.False(AppleMusicAuth.IsAppleMusicJwt(jwt));
    }

    [Fact]
    public void IsAppleMusicJwt_MissingKid_ReturnsFalse()
    {
        var jwt = JwtFaker.Make(
            new { alg = ValidAlg },
            new { iss = TestIssuer, exp = JwtFaker.FutureExp }
        );

        Assert.False(AppleMusicAuth.IsAppleMusicJwt(jwt));
    }

    [Fact]
    public void IsAppleMusicJwt_MissingIss_ReturnsFalse()
    {
        var jwt = JwtFaker.Make(
            new { alg = ValidAlg, kid = TestKid },
            new { exp = JwtFaker.FutureExp }
        );

        Assert.False(AppleMusicAuth.IsAppleMusicJwt(jwt));
    }

    [Fact]
    public void IsAppleMusicJwt_MissingExp_ReturnsFalse()
    {
        var jwt = JwtFaker.Make(new { alg = ValidAlg, kid = TestKid }, new { iss = TestIssuer });

        Assert.False(AppleMusicAuth.IsAppleMusicJwt(jwt));
    }

    [Fact]
    public void IsAppleMusicJwt_ExpiredToken_ReturnsFalse()
    {
        var jwt = JwtFaker.Make(
            new { alg = ValidAlg, kid = TestKid },
            new { iss = TestIssuer, exp = JwtFaker.PastExp }
        );

        Assert.False(AppleMusicAuth.IsAppleMusicJwt(jwt));
    }

    [Fact]
    public void IsAppleMusicJwt_ExpiresInLessThan5Minutes_ReturnsFalse()
    {
        var jwt = JwtFaker.Make(
            new { alg = ValidAlg, kid = TestKid },
            new { iss = TestIssuer, exp = JwtFaker.SoonExp }
        );

        Assert.False(AppleMusicAuth.IsAppleMusicJwt(jwt));
    }

    [Fact]
    public void IsAppleMusicJwt_NotThreeParts_ReturnsFalse()
    {
        Assert.False(AppleMusicAuth.IsAppleMusicJwt("notajwt"));
        Assert.False(AppleMusicAuth.IsAppleMusicJwt("only.two"));
    }

    [Fact]
    public void IsAppleMusicJwt_InvalidBase64_ReturnsFalse()
    {
        Assert.False(AppleMusicAuth.IsAppleMusicJwt("!!!.!!!.!!!"));
    }

    // GetJwtExpiry
    [Fact]
    public void GetJwtExpiry_ValidToken_ReturnsExpiry()
    {
        var expectedExp = DateTimeOffset.UtcNow.AddHours(1);
        var jwt = JwtFaker.Make(
            new { alg = ValidAlg, kid = TestKid },
            new { iss = TestIssuer, exp = expectedExp.ToUnixTimeSeconds() }
        );

        var expiry = AppleMusicAuth.GetJwtExpiry(jwt);

        Assert.NotNull(expiry);
        Assert.Equal(expectedExp.ToUnixTimeSeconds(), expiry.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public void GetJwtExpiry_MissingExp_ReturnsNull()
    {
        var jwt = JwtFaker.Make(new { alg = ValidAlg, kid = TestKid }, new { iss = TestIssuer });

        Assert.Null(AppleMusicAuth.GetJwtExpiry(jwt));
    }

    [Fact]
    public void GetJwtExpiry_NotThreeParts_ReturnsNull()
    {
        Assert.Null(AppleMusicAuth.GetJwtExpiry("not.valid"));
    }

    [Fact]
    public void GetJwtExpiry_InvalidBase64_ReturnsNull()
    {
        Assert.Null(AppleMusicAuth.GetJwtExpiry("!!!.!!!.!!!"));
    }

    [Fact]
    public async Task GetDeveloperTokenAsync_UsesCachedDeveloperTokenWithoutHttpCall()
    {
        using var cachePath = CreateTempTokenPath();
        const string cachedToken = "cached-token";
        var cache = new TokenCache(cachePath.Value)
        {
            DeveloperToken = cachedToken,
            DeveloperTokenExpiry = DateTimeOffset.UtcNow.AddHours(1),
        };
        using var auth = CreateAuth(FakeHttpMessageHandler.ThrowOnCall(), cache);

        var token = await auth.GetDeveloperTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal(cachedToken, token);
    }

    [Fact]
    public async Task GetDeveloperTokenAsync_ExtractsNewestValidTokenFromWebBundles()
    {
        using var cachePath = CreateTempTokenPath();
        var olderExpiry = DateTimeOffset.UtcNow.AddHours(1);
        var newerExpiry = DateTimeOffset.UtcNow.AddHours(2);
        var olderToken = ValidJwt(olderExpiry);
        var newerToken = ValidJwt(newerExpiry);
        using var http = new HttpClient(
            new FakeHttpMessageHandler(request =>
                request.RequestUri?.AbsolutePath switch
                {
                    "/browse" => TextResponse(
                        """
                        <script src="/assets/older.js"></script>
                        <script src="https://music.test/js/newer.js"></script>
                        <script src="/ignored/not-a-bundle.js"></script>
                        """
                    ),
                    "/assets/older.js" => TextResponse($"window.token = '{olderToken}';"),
                    "/js/newer.js" => TextResponse($"window.token = '{newerToken}';"),
                    _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound),
                }
            )
        );
        var cache = new TokenCache(cachePath.Value);
        using var auth = CreateAuth(http, cache);

        var token = await auth.GetDeveloperTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal(newerToken, token);
        Assert.Equal(newerToken, cache.DeveloperToken);
        Assert.Equal(
            newerExpiry.ToUnixTimeSeconds(),
            cache.DeveloperTokenExpiry?.ToUnixTimeSeconds()
        );
        Assert.True(File.Exists(cachePath.Value));
    }

    [Fact]
    public async Task GetDeveloperTokenAsync_SkipsFailedBundlesAndReportsSkippedCount()
    {
        using var cachePath = CreateTempTokenPath();
        using var http = new HttpClient(
            new FakeHttpMessageHandler(request =>
            {
                return request.RequestUri?.AbsolutePath switch
                {
                    "/browse" => TextResponse(
                        """
                        <script src="/assets/failure.js"></script>
                        <script src="/js/no-token.js"></script>
                        """
                    ),
                    "/assets/failure.js" => throw new HttpRequestException("bundle failed"),
                    "/js/no-token.js" => TextResponse("console.log('empty');"),
                    _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound),
                };
            })
        );
        using var auth = CreateAuth(http, new TokenCache(cachePath.Value));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            auth.GetDeveloperTokenAsync(TestContext.Current.CancellationToken)
        );

        Assert.Contains("Skipped 1 failed bundle(s).", error.Message, StringComparison.Ordinal);
    }

    private static AppleMusicAuth CreateAuth(HttpClient http, TokenCache cache) =>
        new(cache, http, Options.Create(new AppleMusicAuthOptions { BaseUrl = BaseUrl }));

    private static HttpResponseMessage TextResponse(string text) =>
        new(System.Net.HttpStatusCode.OK) { Content = new StringContent(text) };

    private static string ValidJwt(DateTimeOffset expiry) =>
        JwtFaker.Make(
            new { alg = ValidAlg, kid = TestKid },
            new { iss = TestIssuer, exp = expiry.ToUnixTimeSeconds() }
        );

    private static TempTokenPath CreateTempTokenPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ciderfy-auth-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return new TempTokenPath(Path.Combine(directory, "tokens.json"));
    }

    private sealed class TempTokenPath(string value) : IDisposable
    {
        public string Value { get; } = value;

        public void Dispose()
        {
            var directory = Path.GetDirectoryName(Value);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
