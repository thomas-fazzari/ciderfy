using System.ComponentModel.DataAnnotations;
using Ciderfy.Configuration;
using Microsoft.Extensions.Options;

namespace Ciderfy.Spotify;

internal sealed class SpotifyClientOptions
{
    public const string SectionName = "Spotify";

    /// <summary>
    /// Spotify web origin used to fetch playlist pages and session data.
    /// </summary>
    [Required]
    [HttpUrl]
    public string WebBaseUrl { get; init; } = "https://open.spotify.com";

    /// <summary>
    /// Spotify partner GraphQL endpoint used to load playlist metadata and tracks.
    /// </summary>
    [Required]
    [HttpUrl]
    public string GraphQlEndpoint { get; init; } =
        "https://api-partner.spotify.com/pathfinder/v2/query";

    /// <summary>
    /// SHA-256 hash of the Spotify persisted GraphQL query used to fetch playlist data.
    /// </summary>
    [Required]
    [RegularExpression("^[a-fA-F0-9]{64}$")]
    public string PlaylistQueryHash { get; init; } =
        "bb67e0af06e8d6f52b531f97468ee4acd44cd0f82b988e15c2ea47b1148efc77";

    /// <summary>
    /// Spotify GraphQL header name used to send the anonymous client token.
    /// </summary>
    [Required]
    [HeaderName]
    public string ClientTokenHeader { get; init; } = "Client-Token";

    /// <summary>
    /// Spotify GraphQL header name used to send the web player app version.
    /// </summary>
    [Required]
    [HeaderName]
    public string AppVersionHeader { get; init; } = "Spotify-App-Version";

    /// <summary>
    /// Spotify client token endpoint used to obtain anonymous web API tokens.
    /// </summary>
    [Required]
    [HttpUrl]
    public string ClientTokenEndpoint { get; init; } =
        "https://clienttoken.spotify.com/v1/clienttoken";

    /// <summary>
    /// Spotify TOTP protocol version sent when requesting a web access token.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int TotpVersion { get; init; } = 61;

    /// <summary>
    /// Maximum duration, in seconds, for Spotify HTTP requests.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; init; } = 30;
}

[OptionsValidator]
internal sealed partial class ValidateSpotifyClientOptions : IValidateOptions<SpotifyClientOptions>;
