using System.ComponentModel.DataAnnotations;

namespace Ciderfy.Configuration.Options;

internal sealed class AppleMusicAuthOptions
{
    public const string SectionName = "AppleMusic:Auth";

    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; init; } = 30;
}
