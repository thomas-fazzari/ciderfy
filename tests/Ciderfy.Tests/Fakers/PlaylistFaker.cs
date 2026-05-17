using Bogus;
using Ciderfy.Spotify;

namespace Ciderfy.Tests.Fakers;

internal static class PlaylistFaker
{
    public static readonly Faker<SpotifyPlaylist> Default =
        new Faker<SpotifyPlaylist>().CustomInstantiator(f =>
        {
            var name = f.Music.Genre() + " " + f.Hacker.Noun();
            var tracks = SpotifyTrackFaker.Default.Generate(f.Random.Int(3, 6));
            return new SpotifyPlaylist(name, tracks);
        });

    public static Faker<SpotifyPlaylist> WithTracks(int trackCount) =>
        new Faker<SpotifyPlaylist>().CustomInstantiator(f =>
        {
            var name = f.Music.Genre() + " " + f.Hacker.Noun();
            var tracks = SpotifyTrackFaker.Default.Generate(trackCount);
            return new SpotifyPlaylist(name, tracks);
        });

    public static Faker<SpotifyPlaylist> WithName(string name) =>
        WithNameAndOptionalTrackCount(name, trackCount: null);

    public static Faker<SpotifyPlaylist> WithNameAndTracks(string name, int trackCount) =>
        WithNameAndOptionalTrackCount(name, trackCount);

    private static Faker<SpotifyPlaylist> WithNameAndOptionalTrackCount(
        string name,
        int? trackCount
    ) =>
        new Faker<SpotifyPlaylist>().CustomInstantiator(f =>
        {
            var tracks = SpotifyTrackFaker.Default.Generate(trackCount ?? f.Random.Int(3, 6));
            return new SpotifyPlaylist(name, tracks);
        });
}
