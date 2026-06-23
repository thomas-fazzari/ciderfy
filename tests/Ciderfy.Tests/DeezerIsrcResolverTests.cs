using Ciderfy.Matching;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ciderfy.Tests;

[IntegrationTest]
public class DeezerIsrcResolverTests
{
    [Fact]
    public async Task FindIsrc_KnownTrack_ReturnsIsrc()
    {
        // Arrange
        using var deezerClient = TestHttpClients.CreateDeezerClient();
        using var resolver = new DeezerIsrcResolver(
            deezerClient,
            Options.Create(new DeezerClientOptions())
        );
        var ct = TestContext.Current.CancellationToken;

        var tracks = new List<TrackMetadata>
        {
            new()
            {
                SpotifyId = "test1",
                Title = "Around the World",
                Artist = "Daft Punk",
                DurationMs = 429000,
            },
        };

        // Act
        var results = await resolver.ResolveIsrcsAsync(tracks, ct: ct);

        // Assert
        Assert.Single(results);
        Assert.NotNull(results[0].Isrc);
        Assert.NotEmpty(results[0].Isrc!);
    }

    [Fact]
    public async Task FindIsrc_MultipleTracks_ResolvesAll()
    {
        // Arrange
        using var deezerClient = TestHttpClients.CreateDeezerClient();
        using var resolver = new DeezerIsrcResolver(
            deezerClient,
            Options.Create(new DeezerClientOptions())
        );
        var ct = TestContext.Current.CancellationToken;

        var tracks = new List<TrackMetadata>
        {
            new()
            {
                SpotifyId = "test1",
                Title = "Revolution 909",
                Artist = "Daft Punk",
                DurationMs = 335000,
            },
            new()
            {
                SpotifyId = "test2",
                Title = "Around the World",
                Artist = "Daft Punk",
                DurationMs = 429000,
            },
        };

        // Act
        var results = await resolver.ResolveIsrcsAsync(tracks, ct: ct);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.NotNull(r.Isrc));
    }

    [Fact]
    public async Task FindIsrc_UnknownTrack_ReturnsNullIsrc()
    {
        // Arrange
        using var deezerClient = TestHttpClients.CreateDeezerClient();
        using var resolver = new DeezerIsrcResolver(
            deezerClient,
            Options.Create(new DeezerClientOptions())
        );
        var ct = TestContext.Current.CancellationToken;

        var tracks = new List<TrackMetadata>
        {
            new()
            {
                SpotifyId = "test1",
                Title = "zzzzzzzzznotarealtrack999",
                Artist = "zzznotarealartist999",
                DurationMs = 0,
            },
        };

        // Act
        var results = await resolver.ResolveIsrcsAsync(tracks, ct: ct);

        // Assert
        Assert.Single(results);
        Assert.Null(results[0].Isrc);
    }
}
