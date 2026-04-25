using System.ComponentModel.DataAnnotations;

namespace Ciderfy.Spotify;

internal sealed class SpotifyClientOptions
{
    public const string SectionName = "Spotify";

    [Required]
    public string WebBaseUrl { get; init; } = "https://open.spotify.com";

    [Required]
    public string GraphQlEndpoint { get; init; } =
        "https://api-partner.spotify.com/pathfinder/v2/query";

    [Required]
    public string ClientTokenEndpoint { get; init; } =
        "https://clienttoken.spotify.com/v1/clienttoken";

    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; init; } = 30;
}
