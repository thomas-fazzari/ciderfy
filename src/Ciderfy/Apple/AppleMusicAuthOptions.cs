using System.ComponentModel.DataAnnotations;

namespace Ciderfy.Apple;

internal sealed class AppleMusicAuthOptions
{
    public const string SectionName = "AppleMusic:Auth";

    [Required]
    public string BaseUrl { get; init; } = "https://music.apple.com";

    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; init; } = 30;
}
