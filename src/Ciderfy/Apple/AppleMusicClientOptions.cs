using System.ComponentModel.DataAnnotations;

namespace Ciderfy.Apple;

internal sealed class AppleMusicClientOptions
{
    public const string SectionName = "AppleMusic:Client";

    [Required]
    public string BaseUrl { get; init; } = "https://api.music.apple.com/v1/";

    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; init; } = 30;

    [Range(0, int.MaxValue)]
    public int MinDelayBetweenCallsMs { get; init; } = 1000;
}
