using System.ComponentModel.DataAnnotations;
using Ciderfy.Configuration;
using Microsoft.Extensions.Options;

namespace Ciderfy.Apple;

internal sealed class AppleMusicAuthOptions
{
    public const string SectionName = "AppleMusic:Auth";

    /// <summary>
    /// Apple Music web origin used to scrape the developer token and set Apple Music request headers.
    /// </summary>
    [Required]
    [HttpUrl]
    public string BaseUrl { get; init; } = "https://music.apple.com";

    /// <summary>
    /// Maximum duration, in seconds, for Apple Music developer token extraction requests.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; init; } = 30;
}

[OptionsValidator]
internal sealed partial class ValidateAppleMusicAuthOptions
    : IValidateOptions<AppleMusicAuthOptions>;
