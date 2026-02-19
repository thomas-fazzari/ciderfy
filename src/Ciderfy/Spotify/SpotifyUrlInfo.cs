namespace Ciderfy.Spotify;

/// <summary>
/// Parses Spotify URLs and URIs into a type and ID
/// </summary>
/// <remarks>
/// Supports standard URLs, embed URLs, internationalized URLs (intl-*), and 'spotify:' URIs
/// </remarks>
internal record SpotifyUrlInfo(SpotifyUrlType Type, string Id)
{
    private const string SpotifyUriScheme = "spotify:";
    private const string SpotifyDomain = "spotify.com";
    private const string EmbedSegment = "embed";
    private const string IntlPrefix = "intl-";

    public static bool TryParse(string? url, out SpotifyUrlInfo? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(url))
            return false;

        result = url.StartsWith(SpotifyUriScheme, StringComparison.OrdinalIgnoreCase)
            ? ParseSpotifyUri(url)
            : ParseSpotifyUrl(url);

        return result is not null;
    }

    public static SpotifyUrlInfo? Parse(string? url) =>
        TryParse(url, out var result) ? result : null;

    private static SpotifyUrlInfo? ParseSpotifyUri(string uri)
    {
        var span = uri.AsSpan();
        var afterScheme = span[SpotifyUriScheme.Length..];

        var separatorIndex = afterScheme.IndexOf(':');
        if (separatorIndex < 1 || separatorIndex >= afterScheme.Length - 1)
            return null;

        var typeSpan = afterScheme[..separatorIndex];
        var id = afterScheme[(separatorIndex + 1)..];

        return TryParseType(typeSpan, out var type)
            ? new SpotifyUrlInfo(type, id.ToString())
            : null;
    }

    private static SpotifyUrlInfo? ParseSpotifyUrl(string url)
    {
        if (
            !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Host.IndexOf(SpotifyDomain, StringComparison.OrdinalIgnoreCase) < 0
        )
            return null;

        var segments = uri.AbsolutePath.AsSpan().Trim('/');

        if (segments.StartsWith(EmbedSegment, StringComparison.OrdinalIgnoreCase))
            segments = segments[EmbedSegment.Length..].TrimStart('/');

        if (segments.StartsWith(IntlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var slashIndex = segments.IndexOf('/');
            if (slashIndex > 0)
                segments = segments[(slashIndex + 1)..];
        }

        var typeEnd = segments.IndexOf('/');
        if (typeEnd < 1)
            return null;

        var typeSpan = segments[..typeEnd];
        var idSegment = segments[(typeEnd + 1)..];

        var idEnd = idSegment.IndexOfAny(['/', '?']);
        var id = idEnd >= 0 ? idSegment[..idEnd] : idSegment;

        if (id.IsEmpty)
            return null;

        return TryParseType(typeSpan, out var type)
            ? new SpotifyUrlInfo(type, id.ToString())
            : null;
    }

    private static bool TryParseType(ReadOnlySpan<char> typeStr, out SpotifyUrlType type) =>
        Enum.TryParse(typeStr, ignoreCase: true, out type);
}
