using System.ComponentModel.DataAnnotations;

namespace Ciderfy.Spotify;

internal sealed class SpotifyClientOptions
{
    public const string SectionName = "Spotify";

    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; init; } = 30;
}
