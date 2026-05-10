using Ciderfy.Apple;
using Ciderfy.Matching;
using Ciderfy.Spotify;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ciderfy.Tests;

public class OptionsValidationTests
{
    [Fact]
    public void DefaultOptions_AreValid()
    {
        Assert.Empty(Validate(new AppleMusicClientOptions()));
        Assert.Empty(Validate(new AppleMusicAuthOptions()));
        Assert.Empty(Validate(new SpotifyClientOptions()));
        Assert.Empty(Validate(new DeezerClientOptions()));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://api.music.apple.com/v1/")]
    [InlineData("https://api.music.apple.com/v1")]
    public void AppleMusicClientOptions_InvalidBaseUrl_ReturnsValidationError(string baseUrl)
    {
        var results = Validate(new AppleMusicClientOptions { BaseUrl = baseUrl });

        Assert.Contains(
            results,
            failure =>
                failure.Contains(nameof(AppleMusicClientOptions.BaseUrl), StringComparison.Ordinal)
        );
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://music.apple.com")]
    public void AppleMusicAuthOptions_InvalidBaseUrl_ReturnsValidationError(string baseUrl)
    {
        var results = Validate(new AppleMusicAuthOptions { BaseUrl = baseUrl });

        Assert.Contains(
            results,
            failure =>
                failure.Contains(nameof(AppleMusicAuthOptions.BaseUrl), StringComparison.Ordinal)
        );
    }

    [Fact]
    public void SpotifyClientOptions_InvalidEndpoint_ReturnsValidationError()
    {
        var results = Validate(new SpotifyClientOptions { GraphQlEndpoint = "file:///tmp" });

        Assert.Contains(
            results,
            failure =>
                failure.Contains(
                    nameof(SpotifyClientOptions.GraphQlEndpoint),
                    StringComparison.Ordinal
                )
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("gg67e0af06e8d6f52b531f97468ee4acd44cd0f82b988e15c2ea47b1148efc77")]
    public void SpotifyClientOptions_InvalidPlaylistQueryHash_ReturnsValidationError(
        string playlistQueryHash
    )
    {
        var results = Validate(new SpotifyClientOptions { PlaylistQueryHash = playlistQueryHash });

        Assert.Contains(
            results,
            failure =>
                failure.Contains(
                    nameof(SpotifyClientOptions.PlaylistQueryHash),
                    StringComparison.Ordinal
                )
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SpotifyClientOptions_InvalidTotpVersion_ReturnsValidationError(int totpVersion)
    {
        var results = Validate(new SpotifyClientOptions { TotpVersion = totpVersion });

        Assert.Contains(
            results,
            failure =>
                failure.Contains(nameof(SpotifyClientOptions.TotpVersion), StringComparison.Ordinal)
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("bad header")]
    [InlineData("Bad:Header")]
    public void SpotifyClientOptions_InvalidHeaderNames_ReturnsValidationError(string? headerName)
    {
        var clientTokenResults = Validate(
            new SpotifyClientOptions { ClientTokenHeader = headerName! }
        );
        var appVersionResults = Validate(
            new SpotifyClientOptions { AppVersionHeader = headerName! }
        );

        Assert.Contains(
            clientTokenResults,
            failure =>
                failure.Contains(
                    nameof(SpotifyClientOptions.ClientTokenHeader),
                    StringComparison.Ordinal
                )
        );
        Assert.Contains(
            appVersionResults,
            failure =>
                failure.Contains(
                    nameof(SpotifyClientOptions.AppVersionHeader),
                    StringComparison.Ordinal
                )
        );
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://api.deezer.com/")]
    [InlineData("https://api.deezer.com")]
    public void DeezerClientOptions_InvalidBaseUrl_ReturnsValidationError(string baseUrl)
    {
        var results = Validate(new DeezerClientOptions { BaseUrl = baseUrl });

        Assert.Contains(
            results,
            failure =>
                failure.Contains(nameof(DeezerClientOptions.BaseUrl), StringComparison.Ordinal)
        );
    }

    private static string[] Validate(AppleMusicClientOptions options) =>
        GetFailures(new ValidateAppleMusicClientOptions().Validate(null, options));

    private static string[] Validate(AppleMusicAuthOptions options) =>
        GetFailures(new ValidateAppleMusicAuthOptions().Validate(null, options));

    private static string[] Validate(SpotifyClientOptions options) =>
        GetFailures(new ValidateSpotifyClientOptions().Validate(null, options));

    private static string[] Validate(DeezerClientOptions options) =>
        GetFailures(new ValidateDeezerClientOptions().Validate(null, options));

    private static string[] GetFailures(ValidateOptionsResult result) =>
        result.Failed ? result.Failures.ToArray() : [];
}
