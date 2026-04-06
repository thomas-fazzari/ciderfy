namespace Ciderfy.Spotify;

internal record SpotifyPlaylist(string Name, IReadOnlyList<SpotifyTrack> Tracks);
