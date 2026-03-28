using Ciderfy.Apple;
using Ciderfy.Tests.Fakers;
using Xunit;

namespace Ciderfy.Tests;

public class AppleMusicAuthTests
{
    private const string ValidAlg = "ES256";
    private const string TestKid = "ABCD1234";
    private const string TestIssuer = "TEAMID";

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
}
