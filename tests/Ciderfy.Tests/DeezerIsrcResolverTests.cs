using Ciderfy.Matching;
using Xunit;

namespace Ciderfy.Tests;

[IntegrationTest]
public class DeezerIsrcResolverTests
{
    [Fact]
    public async Task FindIsrc_KnownTrack_ReturnsIsrc()
    {
        // Arrange
        using var resolver = new DeezerIsrcResolver();
        var ct = TestContext.Current.CancellationToken;

        var tracks = new List<TrackMetadata>
        {
            new()
            {
                SpotifyId = "test1",
                Title = "Around the World",
                Artist = "Daft Punk",
                DurationMs = 420000,
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
        using var resolver = new DeezerIsrcResolver();
        var ct = TestContext.Current.CancellationToken;

        var tracks = new List<TrackMetadata>
        {
            new()
            {
                SpotifyId = "test1",
                Title = "Revolution 909",
                Artist = "Daft Punk",
                DurationMs = 330000,
            },
            new()
            {
                SpotifyId = "test2",
                Title = "Bohemian Rhapsody",
                Artist = "Queen",
                DurationMs = 355000,
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
        using var resolver = new DeezerIsrcResolver();
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
