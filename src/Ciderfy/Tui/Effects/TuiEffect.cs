using Ciderfy.Matching;

namespace Ciderfy.Tui;

/// <summary>
/// Side effect requested by the TUI controller and executed outside state updates
/// </summary>
internal interface ITuiEffect;

/// <summary>
/// Stops the TUI application
/// </summary>
internal sealed record QuitAppEffect : ITuiEffect;

/// <summary>
/// Starts Apple Music authentication
/// </summary>
internal sealed record StartAuthEffect : ITuiEffect;

/// <summary>
/// Opens the Ciderfy configuration folder
/// </summary>
internal sealed record OpenConfigEffect : ITuiEffect;

/// <summary>
/// Cancels the active playlist transfer
/// </summary>
internal sealed record CancelCurrentTransferEffect : ITuiEffect;

/// <summary>
/// Fetches Spotify playlists for a transfer
/// </summary>
internal sealed record StartFetchPlaylistsEffect(int TransferId, IReadOnlyList<string> PlaylistIds)
    : ITuiEffect;

/// <summary>
/// Matches tracks through ISRC lookup
/// </summary>
internal sealed record StartIsrcMatchEffect(
    int TransferId,
    IReadOnlyList<TrackMetadata> Tracks,
    string Storefront
) : ITuiEffect;

/// <summary>
/// Matches remaining tracks by text search
/// </summary>
internal sealed record StartTextMatchEffect(
    int TransferId,
    IReadOnlyList<TrackMetadata> Tracks,
    string Storefront
) : ITuiEffect;

/// <summary>
/// Creates the Apple Music playlist from transfer results
/// </summary>
internal sealed record StartCreatePlaylistEffect(
    int TransferId,
    string PlaylistName,
    IReadOnlyList<MatchResult> AllResults
) : ITuiEffect;
