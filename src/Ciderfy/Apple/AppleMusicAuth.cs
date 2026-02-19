using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ciderfy.Apple;

/// <summary>
/// Extracts the Apple Music developer JWT from the web player's JavaScript bundles
/// </summary>
/// <remarks>
/// Token is cached in <see cref="TokenCache"/> and reused until it expires
/// </remarks>
internal sealed partial class AppleMusicAuth(TokenCache tokenCache) : IDisposable
{
    private const string AppleMusicUrl = "https://music.apple.com";
    private const string UserAgent =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private readonly HttpClient _httpClient = CreateHttpClient();

    /// <returns>
    /// A valid developer token, using the cache if available or scraping it from the web player
    /// </returns>
    public async Task<string> GetDeveloperTokenAsync(CancellationToken ct = default)
    {
        if (tokenCache.HasValidDeveloperToken)
            return tokenCache.DeveloperToken!;

        var token = await ExtractDeveloperTokenFromWebAsync(ct);

        tokenCache.DeveloperToken = token;
        tokenCache.DeveloperTokenExpiry = GetJwtExpiry(token) ?? DateTimeOffset.UtcNow.AddHours(12);
        tokenCache.Save();

        return token;
    }

    /// <summary>
    /// Scrapes the Apple Music /browse page and finds JS bundles containing an ES256 JWT
    /// </summary>
    /// <returns>
    /// The first valid token found in the bundles
    /// </returns>
    /// <remarks>
    /// Note: might break if Apple changes the web player structure
    /// </remarks>
    private async Task<string> ExtractDeveloperTokenFromWebAsync(CancellationToken ct)
    {
        var html = await _httpClient.GetStringAsync($"{AppleMusicUrl}/browse", ct);

        var scriptUrls = ScriptSrcRegex()
            .Matches(html)
            .Select(m => m.Groups[1].Value)
            .Where(url => url.Contains("assets/") || url.Contains("js/"))
            .ToList();

        foreach (var scriptUrl in scriptUrls)
        {
            ct.ThrowIfCancellationRequested();

            var fullUrl = scriptUrl.StartsWith("http") ? scriptUrl : $"{AppleMusicUrl}{scriptUrl}";

            try
            {
                var js = await _httpClient.GetStringAsync(fullUrl, ct);
                var jwtMatch = JwtTokenRegex().Match(js);

                if (!jwtMatch.Success)
                    continue;

                var token = jwtMatch.Value;
                if (IsAppleMusicJwt(token))
                    return token;
            }
            catch
            {
                // Skip failed bundles and try next
            }
        }

        throw new InvalidOperationException(
            "Could not extract Apple Music developer token from web player. "
                + "Apple may have changed their web player structure."
        );
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        return client;
    }

    private static bool IsAppleMusicJwt(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return false;

            var headerJson = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[0]));
            using var header = JsonDocument.Parse(headerJson);

            return header.RootElement.TryGetProperty("alg", out var alg)
                && alg.GetString() == "ES256";
        }
        catch
        {
            return false;
        }
    }

    private static DateTimeOffset? GetJwtExpiry(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return null;

            var payloadJson = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[1]));
            using var payload = JsonDocument.Parse(payloadJson);

            if (payload.RootElement.TryGetProperty("exp", out var exp))
                return DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());

            return null;
        }
        catch
        {
            return null;
        }
    }

    [GeneratedRegex(@"<script[^>]+src=""([^""]+\.js[^""]*)""")]
    private static partial Regex ScriptSrcRegex();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{20,}\.eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}")]
    private static partial Regex JwtTokenRegex();

    public void Dispose() => _httpClient.Dispose();
}
