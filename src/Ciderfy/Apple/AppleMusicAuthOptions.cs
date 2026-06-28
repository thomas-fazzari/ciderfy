using System.ComponentModel.DataAnnotations;

namespace Ciderfy.Apple;

internal sealed class AppleMusicAuthOptions
{
    public const string SectionName = "AppleMusic:Auth";

    /// <summary>
    /// Maximum duration, in seconds, for Apple Music developer token extraction requests.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; init; } = 30;
}
