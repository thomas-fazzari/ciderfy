using System.ComponentModel.DataAnnotations;
using Ciderfy.Configuration;
using Microsoft.Extensions.Options;

namespace Ciderfy.Matching;

internal sealed class DeezerClientOptions
{
    public const string SectionName = "Deezer";

    /// <summary>
    /// Deezer API base URL used for ISRC lookup requests.
    /// </summary>
    [Required]
    [HttpUrl]
    [EndsWithSlash]
    public string BaseUrl { get; init; } = "https://api.deezer.com/";

    /// <summary>
    /// Maximum duration, in seconds, for Deezer API requests.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; init; } = 15;

    /// <summary>
    /// Minimum delay, in milliseconds, enforced between Deezer ISRC lookup calls.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int RateLimitDelayMs { get; init; } = 110;
}

[OptionsValidator]
internal sealed partial class ValidateDeezerClientOptions : IValidateOptions<DeezerClientOptions>;
