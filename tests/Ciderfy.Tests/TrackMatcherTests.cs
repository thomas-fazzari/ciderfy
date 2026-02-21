using Bogus;
using Ciderfy.Matching;
using Ciderfy.Tests.Fakers;
using Xunit;

namespace Ciderfy.Tests;

public class TrackMatcherTests
{
    // StripVersionSuffix
    [Theory]
    [InlineData("Time Of The Season - Mono", "Time Of The Season")]
    [InlineData("Suzie Q (Remastered 2014)", "Suzie Q")]
    [InlineData("Green Grass & High Tides - Remastered", "Green Grass & High Tides")]
    [InlineData("Goin' Up The Country - Re-Recorded", "Goin' Up The Country")]
    [InlineData("Born To Be Wild - Single Version", "Born To Be Wild")]
    [InlineData("American Woman - 2024 Remaster", "American Woman")]
    [InlineData("Hello Vietnam", "Hello Vietnam")]
    [InlineData("Fortunate Son - Remastered 2014", "Fortunate Son")]
    [InlineData("Paint It Black (Mono)", "Paint It Black")]
    [InlineData("Somebody to Love [Live]", "Somebody to Love")]
    [InlineData("White Rabbit - Stereo Version", "White Rabbit")]
    [InlineData("Gimme Shelter (Original Mix)", "Gimme Shelter")]
    [InlineData("Purple Haze - Deluxe Edition", "Purple Haze")]
    [InlineData("Hey Joe - Remix", "Hey Joe")]
    [InlineData("Light My Fire (Bonus Track)", "Light My Fire")]
    [InlineData("All Along the Watchtower (feat. Jimi Hendrix)", "All Along the Watchtower")]
    [InlineData("Sunshine of Your Love (ft. Eric Clapton)", "Sunshine of Your Love")]
    [InlineData("Break On Through — Remastered", "Break On Through")]
    [InlineData("Piece of My Heart – Live at Winterland", "Piece of My Heart")]
    [InlineData("Already Gone", "Already Gone")]
    public void StripVersionSuffix_StripsCorrectly(string input, string expected)
    {
        var result = TrackMatcher.StripVersionSuffix(input);

        Assert.Equal(expected, result);
    }

    // NormalizeForComparison
    [Theory]
    [InlineData("Don't Stop (Remix) [Deluxe Edition]", "dont stop")]
    [InlineData("We've Gotta Get out of This Place", "weve gotta get out of this place")]
    [InlineData("Rock & Roll", "rock and roll")]
    [InlineData("HELLO WORLD", "hello world")]
    [InlineData("Hush – Deep Purple", "hush - deep purple")]
    [InlineData("My Girl\u2019s Name", "my girls name")]
    [InlineData("Song feat. Artist", "song")]
    [InlineData("Song ft. Other Artist", "song")]
    [InlineData("  spaces  everywhere  ", "spaces  everywhere")]
    public void NormalizeForComparison_NormalizesCorrectly(string input, string expected)
    {
        var result = TrackMatcher.NormalizeForComparison(input);

        Assert.Equal(expected, result);
    }

    // TitleSimilarity
    [Theory]
    [InlineData("Suzie Q", "Suzie Q", 1.0)]
    [InlineData("Time Of The Season - Mono", "Time Of The Season", 1.0)]
    [InlineData("War Pigs", "War Pigs / Luke's Wall", 0.9)]
    [InlineData("Fortunate Son", "Fortunate Son - Remastered 2014", 1.0)]
    [InlineData("Paint It Black", "Paint It, Black", 1.0)]
    [InlineData("Revolution 909", "Revolution 909", 1.0)]
    [InlineData("Hey Jude", "Let It Be", -1.0)] // different songs -> below 0.7
    public void TitleSimilarity_ReturnsExpectedScore(string a, string b, double expected)
    {
        var result = TrackMatcher.TitleSimilarity(a, b);

        if (expected < 0)
            Assert.True(result < 0.7, $"Expected < 0.7 but got {result}");
        else
            Assert.Equal(expected, result, precision: 1);
    }

    [Fact]
    public void TitleSimilarity_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, TrackMatcher.TitleSimilarity("", "Something"));
        Assert.Equal(0, TrackMatcher.TitleSimilarity("Something", ""));
        Assert.Equal(0, TrackMatcher.TitleSimilarity("", ""));
    }

    // ArtistSimilarity
    [Theory]
    [InlineData("The Animals", "Animals", 1.0)]
    [InlineData("Eric Burdon & the Animals", "Eric Burdon and The Animals", 1.0)]
    [InlineData("Daft Punk", "Daft Punk", 1.0)]
    [InlineData("The Rolling Stones", "Rolling Stones", 1.0)]
    [InlineData("Creedence Clearwater Revival", "Creedence Clearwater Revival", 1.0)]
    [InlineData("The Jimi Hendrix Experience", "Jimi Hendrix", 0.9)]
    [InlineData("Simon & Garfunkel", "Simon and Garfunkel", 1.0)]
    [InlineData("Crosby, Stills, Nash & Young", "Crosby, Stills, Nash and Young", 1.0)]
    [InlineData("Daft Punk", "Queen", -1.0)] // completely different
    public void ArtistSimilarity_ReturnsExpectedScore(string a, string b, double expected)
    {
        var result = TrackMatcher.ArtistSimilarity(a, b);

        if (expected < 0)
            Assert.True(result < 0.7, $"Expected < 0.7 but got {result}");
        else
            Assert.Equal(expected, result, precision: 1);
    }

    [Fact]
    public void ArtistSimilarity_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, TrackMatcher.ArtistSimilarity("", "Daft Punk"));
        Assert.Equal(0, TrackMatcher.ArtistSimilarity("Daft Punk", ""));
    }

    // CalculateSimilarity
    [Fact]
    public void CalculateSimilarity_IdenticalTracks_ReturnsOne()
    {
        var spotify = TrackFakers
            .SpotifyTrack.Clone()
            .RuleFor(t => t.Title, "Revolution 909")
            .RuleFor(t => t.Artist, "Daft Punk")
            .Generate();
        var apple = TrackFakers
            .AppleTrack.Clone()
            .RuleFor(t => t.Title, "Revolution 909")
            .RuleFor(t => t.Artist, "Daft Punk")
            .Generate();

        Assert.Equal(1.0, TrackMatcher.CalculateSimilarity(spotify, apple), precision: 2);
    }

    [Fact]
    public void CalculateSimilarity_RandomIdenticalTracks_ReturnsOne()
    {
        var faker = new Faker();
        var title = faker.Random.Words(3);
        var artist = faker.Person.FullName;

        var spotify = TrackFakers
            .SpotifyTrack.Clone()
            .RuleFor(t => t.Title, title)
            .RuleFor(t => t.Artist, artist)
            .Generate();
        var apple = TrackFakers
            .AppleTrack.Clone()
            .RuleFor(t => t.Title, title)
            .RuleFor(t => t.Artist, artist)
            .Generate();

        Assert.Equal(1.0, TrackMatcher.CalculateSimilarity(spotify, apple), precision: 2);
    }

    [Fact]
    public void CalculateSimilarity_TwoRandomTracks_ReturnsBelowThreshold()
    {
        var spotify = TrackFakers.SpotifyTrack.Generate();
        var apple = TrackFakers.AppleTrack.Generate();

        var result = TrackMatcher.CalculateSimilarity(spotify, apple);

        Assert.True(result < 0.95, $"Random tracks scored suspiciously high: {result}");
    }

    [Fact]
    public void CalculateSimilarity_DifferentTracks_ReturnsBelowThreshold()
    {
        var spotify = TrackFakers
            .SpotifyTrack.Clone()
            .RuleFor(t => t.Title, "Revolution 909")
            .RuleFor(t => t.Artist, "Daft Punk")
            .Generate();
        var apple = TrackFakers
            .AppleTrack.Clone()
            .RuleFor(t => t.Title, "Bohemian Rhapsody")
            .RuleFor(t => t.Artist, "Queen")
            .Generate();

        var result = TrackMatcher.CalculateSimilarity(spotify, apple);
        Assert.True(result < 0.7, $"Expected < 0.7 but got {result}");
    }

    [Fact]
    public void CalculateSimilarity_SameSongDifferentVersions_AboveThreshold()
    {
        var spotify = TrackFakers
            .SpotifyTrack.Clone()
            .RuleFor(t => t.Title, "Fortunate Son - Remastered 2014")
            .RuleFor(t => t.Artist, "Creedence Clearwater Revival")
            .Generate();
        var apple = TrackFakers
            .AppleTrack.Clone()
            .RuleFor(t => t.Title, "Fortunate Son")
            .RuleFor(t => t.Artist, "Creedence Clearwater Revival")
            .Generate();

        var result = TrackMatcher.CalculateSimilarity(spotify, apple);
        Assert.True(result >= 0.7, $"Expected >= 0.7 but got {result}");
    }

    [Fact]
    public void CalculateSimilarity_SameArtistDifferentSong_BelowThreshold()
    {
        var spotify = TrackFakers
            .SpotifyTrack.Clone()
            .RuleFor(t => t.Title, "Purple Haze")
            .RuleFor(t => t.Artist, "Jimi Hendrix")
            .Generate();
        var apple = TrackFakers
            .AppleTrack.Clone()
            .RuleFor(t => t.Title, "Voodoo Child")
            .RuleFor(t => t.Artist, "Jimi Hendrix")
            .Generate();

        var result = TrackMatcher.CalculateSimilarity(spotify, apple);
        Assert.True(result < 0.7, $"Expected < 0.7 but got {result}");
    }

    [Fact]
    public void CalculateSimilarity_ThePrefix_StillMatches()
    {
        var spotify = TrackFakers
            .SpotifyTrack.Clone()
            .RuleFor(t => t.Title, "House of the Rising Sun")
            .RuleFor(t => t.Artist, "The Animals")
            .Generate();
        var apple = TrackFakers
            .AppleTrack.Clone()
            .RuleFor(t => t.Title, "House of the Rising Sun")
            .RuleFor(t => t.Artist, "Animals")
            .Generate();

        var result = TrackMatcher.CalculateSimilarity(spotify, apple);
        Assert.True(result >= 0.9, $"Expected >= 0.9 but got {result}");
    }

    [Fact]
    public void CalculateSimilarity_AmpersandVsAnd_StillMatches()
    {
        var spotify = TrackFakers
            .SpotifyTrack.Clone()
            .RuleFor(t => t.Title, "The Sound of Silence")
            .RuleFor(t => t.Artist, "Simon & Garfunkel")
            .Generate();
        var apple = TrackFakers
            .AppleTrack.Clone()
            .RuleFor(t => t.Title, "The Sound of Silence")
            .RuleFor(t => t.Artist, "Simon and Garfunkel")
            .Generate();

        var result = TrackMatcher.CalculateSimilarity(spotify, apple);
        Assert.True(result >= 0.9, $"Expected >= 0.9 but got {result}");
    }

    [Fact]
    public void CalculateSimilarity_SlashTitle_StillMatches()
    {
        var spotify = TrackFakers
            .SpotifyTrack.Clone()
            .RuleFor(t => t.Title, "War Pigs")
            .RuleFor(t => t.Artist, "Black Sabbath")
            .Generate();
        var apple = TrackFakers
            .AppleTrack.Clone()
            .RuleFor(t => t.Title, "War Pigs / Luke's Wall")
            .RuleFor(t => t.Artist, "Black Sabbath")
            .Generate();

        var result = TrackMatcher.CalculateSimilarity(spotify, apple);
        Assert.True(result >= 0.7, $"Expected >= 0.7 but got {result}");
    }

    [Fact]
    public void CalculateSimilarity_FeatInTitle_StillMatches()
    {
        var spotify = TrackFakers
            .SpotifyTrack.Clone()
            .RuleFor(t => t.Title, "Something feat. George Harrison")
            .RuleFor(t => t.Artist, "The Beatles")
            .Generate();
        var apple = TrackFakers
            .AppleTrack.Clone()
            .RuleFor(t => t.Title, "Something")
            .RuleFor(t => t.Artist, "The Beatles")
            .Generate();

        var result = TrackMatcher.CalculateSimilarity(spotify, apple);
        Assert.True(result >= 0.9, $"Expected >= 0.9 but got {result}");
    }

    [Fact]
    public void CalculateSimilarity_VietnamWarPlaylistEdgeCases()
    {
        // Apostrophe normalization
        var spotify1 = TrackFakers
            .SpotifyTrack.Clone()
            .RuleFor(t => t.Title, "We've Gotta Get out of This Place")
            .RuleFor(t => t.Artist, "The Animals")
            .Generate();
        var apple1 = TrackFakers
            .AppleTrack.Clone()
            .RuleFor(t => t.Title, "We Gotta Get out of This Place")
            .RuleFor(t => t.Artist, "The Animals")
            .Generate();

        var result1 = TrackMatcher.CalculateSimilarity(spotify1, apple1);
        Assert.True(result1 >= 0.7, $"Expected >= 0.7 but got {result1}");

        // With remaster suffix
        var spotify2 = TrackFakers
            .SpotifyTrack.Clone()
            .RuleFor(t => t.Title, "Green Grass & High Tides - Remastered")
            .RuleFor(t => t.Artist, "The Outlaws")
            .Generate();
        var apple2 = TrackFakers
            .AppleTrack.Clone()
            .RuleFor(t => t.Title, "Green Grass and High Tides")
            .RuleFor(t => t.Artist, "The Outlaws")
            .Generate();

        var result2 = TrackMatcher.CalculateSimilarity(spotify2, apple2);
        Assert.True(result2 >= 0.7, $"Expected >= 0.7 but got {result2}");
    }

    [Theory]
    [InlineData(240_000, 240_000, 1.0)]
    [InlineData(240_000, 243_000, 1.0)] // 3s
    [InlineData(240_000, 250_000, 0.95)] // 10s
    [InlineData(240_000, 260_000, 0.90)] // 20s
    [InlineData(240_000, 290_000, 0.80)] // 50s
    [InlineData(240_000, 340_000, 0.70)] // 100s
    [InlineData(0, 240_000, 1.0)] // unknown spotify duration
    [InlineData(240_000, 0, 1.0)] // unknown apple duration
    [InlineData(0, 0, 1.0)] // both unknown
    public void DurationMultiplier_ReturnsExpectedValue(int spotifyMs, int appleMs, double expected)
    {
        Assert.Equal(expected, TrackMatcher.DurationMultiplier(spotifyMs, appleMs));
    }

    [Fact]
    public void CalculateSimilarity_IdenticalSong_LargeDurationDiff_PenalizesScore()
    {
        var spotify = TrackFakers
            .SpotifyTrackWithDuration(240_000)
            .RuleFor(t => t.Title, "Song A")
            .RuleFor(t => t.Artist, "Artist A")
            .Generate();

        var apple = TrackFakers
            .AppleTrackWithDuration(420_000)
            .RuleFor(t => t.Title, "Song A")
            .RuleFor(t => t.Artist, "Artist A")
            .Generate();

        var result = TrackMatcher.CalculateSimilarity(spotify, apple);
        Assert.Equal(0.70, result, precision: 2);
    }

    [Fact]
    public void CalculateSimilarity_ModerateDurationDiff_CanDropBelowThreshold()
    {
        // Borderline text match with a duration difference that should drop below 0.7
        var spotify = TrackFakers
            .SpotifyTrackWithDuration(200_000)
            .RuleFor(t => t.Title, "War Pigs")
            .RuleFor(t => t.Artist, "Black Sabbath")
            .Generate();
        var apple = TrackFakers
            .AppleTrackWithDuration(245_000) // 45s
            .RuleFor(t => t.Title, "War Pigs / Luke's Wall")
            .RuleFor(t => t.Artist, "Black Sabbath")
            .Generate();

        var textOnly = TrackMatcher.CalculateSimilarity(
            TrackFakers
                .SpotifyTrack.Clone()
                .RuleFor(t => t.Title, "War Pigs")
                .RuleFor(t => t.Artist, "Black Sabbath")
                .Generate(),
            TrackFakers
                .AppleTrack.Clone()
                .RuleFor(t => t.Title, "War Pigs / Luke's Wall")
                .RuleFor(t => t.Artist, "Black Sabbath")
                .Generate()
        );
        var withDuration = TrackMatcher.CalculateSimilarity(spotify, apple);

        Assert.True(withDuration < textOnly, $"Expected {withDuration} < {textOnly}");
    }
}
