using System.Text;
using System.Text.Json;
using Ciderfy.Apple;
using Xunit;

namespace Ciderfy.Tests;

public class AppleMusicAuthTests
{
    // Builds a minimal base64url-encoded JWT: header.payload.signature
    private static string MakeJwt(object header, object payload)
    {
        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);
        return $"{B64Url(headerJson)}.{B64Url(payloadJson)}.fakesig";
    }

    private static string B64Url(string json) =>
        Convert
            .ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static long FutureExp => DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
    private static long PastExp => DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
    private static long SoonExp => DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeSeconds();

    [Fact]
    public void IsAppleMusicJwt_ValidToken_ReturnsTrue()
    {
        var jwt = MakeJwt(
            new { alg = "ES256", kid = "ABCD1234" },
            new { iss = "TEAMID", exp = FutureExp }
        );

        Assert.True(AppleMusicAuth.IsAppleMusicJwt(jwt));
    }

    [Fact]
    public void IsAppleMusicJwt_WrongAlgorithm_ReturnsFalse()
    {
        var jwt = MakeJwt(
            new { alg = "RS256", kid = "ABCD1234" },
            new { iss = "TEAMID", exp = FutureExp }
        );

        Assert.False(AppleMusicAuth.IsAppleMusicJwt(jwt));
    }

    [Fact]
    public void IsAppleMusicJwt_MissingKid_ReturnsFalse()
    {
        var jwt = MakeJwt(new { alg = "ES256" }, new { iss = "TEAMID", exp = FutureExp });

        Assert.False(AppleMusicAuth.IsAppleMusicJwt(jwt));
    }

    [Fact]
    public void IsAppleMusicJwt_MissingIss_ReturnsFalse()
    {
        var jwt = MakeJwt(new { alg = "ES256", kid = "ABCD1234" }, new { exp = FutureExp });

        Assert.False(AppleMusicAuth.IsAppleMusicJwt(jwt));
    }

    [Fact]
    public void IsAppleMusicJwt_MissingExp_ReturnsFalse()
    {
        var jwt = MakeJwt(new { alg = "ES256", kid = "ABCD1234" }, new { iss = "TEAMID" });

        Assert.False(AppleMusicAuth.IsAppleMusicJwt(jwt));
    }

    [Fact]
    public void IsAppleMusicJwt_ExpiredToken_ReturnsFalse()
    {
        var jwt = MakeJwt(
            new { alg = "ES256", kid = "ABCD1234" },
            new { iss = "TEAMID", exp = PastExp }
        );

        Assert.False(AppleMusicAuth.IsAppleMusicJwt(jwt));
    }

    [Fact]
    public void IsAppleMusicJwt_ExpiresInLessThan5Minutes_ReturnsFalse()
    {
        var jwt = MakeJwt(
            new { alg = "ES256", kid = "ABCD1234" },
            new { iss = "TEAMID", exp = SoonExp }
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
        var jwt = MakeJwt(
            new { alg = "ES256", kid = "ABCD1234" },
            new { iss = "TEAMID", exp = expectedExp.ToUnixTimeSeconds() }
        );

        var expiry = AppleMusicAuth.GetJwtExpiry(jwt);

        Assert.NotNull(expiry);
        Assert.Equal(expectedExp.ToUnixTimeSeconds(), expiry.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public void GetJwtExpiry_MissingExp_ReturnsNull()
    {
        var jwt = MakeJwt(new { alg = "ES256", kid = "ABCD1234" }, new { iss = "TEAMID" });

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
}
