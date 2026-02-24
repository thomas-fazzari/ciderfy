using System.ComponentModel.DataAnnotations;

namespace Ciderfy.Configuration.Options;

internal sealed class AppleMusicClientOptions
{
    public const string SectionName = "AppleMusic:Client";

    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; init; } = 30;

    [Range(0, int.MaxValue)]
    public int MinDelayBetweenCallsMs { get; init; } = 1000;
}
