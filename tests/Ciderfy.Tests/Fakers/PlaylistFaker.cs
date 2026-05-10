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
        new Faker<SpotifyPlaylist>().CustomInstantiator(f =>
        {
            var tracks = SpotifyTrackFaker.Default.Generate(f.Random.Int(3, 6));
            return new SpotifyPlaylist(name, tracks);
        });
}
