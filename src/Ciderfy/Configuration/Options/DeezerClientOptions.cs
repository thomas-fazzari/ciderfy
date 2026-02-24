using System.ComponentModel.DataAnnotations;

namespace Ciderfy.Configuration.Options;

internal sealed class DeezerClientOptions
{
    public const string SectionName = "Deezer";

    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; init; } = 15;

    [Range(0, int.MaxValue)]
    public int RateLimitDelayMs { get; init; } = 110;
}
