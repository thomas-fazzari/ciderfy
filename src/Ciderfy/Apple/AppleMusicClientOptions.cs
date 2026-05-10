using System.ComponentModel.DataAnnotations;
using Ciderfy.Configuration;
using Microsoft.Extensions.Options;

namespace Ciderfy.Apple;

internal sealed class AppleMusicClientOptions
{
    public const string SectionName = "AppleMusic:Client";

    /// <summary>
    /// Apple Music API base URL used for catalog search and playlist creation requests.
    /// </summary>
    [Required]
    [HttpUrl]
    [EndsWithSlash]
    public string BaseUrl { get; init; } = "https://api.music.apple.com/v1/";

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

[OptionsValidator]
internal sealed partial class ValidateAppleMusicClientOptions
    : IValidateOptions<AppleMusicClientOptions>;
