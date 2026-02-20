namespace Ciderfy.Configuration;

internal sealed class AppleMusicClientOptions
{
    public int TimeoutSeconds { get; init; } = 30;
    public int MinDelayBetweenCallsMs { get; init; } = 1000;
}

internal sealed class AppleMusicAuthOptions
{
    public int TimeoutSeconds { get; init; } = 30;
}

internal sealed class DeezerClientOptions
{
    public int TimeoutSeconds { get; init; } = 15;
    public int RateLimitDelayMs { get; init; } = 110;
}

internal sealed class SpotifyClientOptions
{
    public int TimeoutSeconds { get; init; } = 30;
}
