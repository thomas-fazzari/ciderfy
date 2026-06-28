using System.ComponentModel.DataAnnotations;

namespace Ciderfy.Apple;

internal sealed class AppleMusicClientOptions
{
    public const string SectionName = "AppleMusic:Client";

    /// <summary>
    /// Maximum duration, in seconds, for Apple Music API requests.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Minimum delay, in milliseconds, enforced between Apple Music API calls.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MinDelayBetweenCallsMs { get; init; } = 1000;
}
