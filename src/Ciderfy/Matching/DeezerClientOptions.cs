using System.ComponentModel.DataAnnotations;

namespace Ciderfy.Matching;

internal sealed class DeezerClientOptions
{
    public const string SectionName = "Deezer";

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
