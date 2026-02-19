using Ciderfy.Matching;

namespace Ciderfy.Spotify;

internal record SpotifyPlaylist(string Name, IReadOnlyList<TrackMetadata> Tracks);
