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
internal sealed partial class AppleMusicAuth(TokenCache tokenCache, HttpClient httpClient)
{
    private const string AppleMusicUrl = "https://music.apple.com";
    private readonly TokenCache _tokenCache = tokenCache;
    private readonly HttpClient _httpClient = httpClient;

    /// <returns>
    /// A valid developer token, using the cache if available or scraping it from the web player
    /// </returns>
    public async Task<string> GetDeveloperTokenAsync(CancellationToken ct = default)
    {
        if (_tokenCache.HasValidDeveloperToken)
            return _tokenCache.DeveloperToken!;

        var token = await ExtractDeveloperTokenFromWebAsync(ct);

        _tokenCache.DeveloperToken = token;
        _tokenCache.DeveloperTokenExpiry =
            GetJwtExpiry(token) ?? DateTimeOffset.UtcNow.AddHours(12);
        _tokenCache.Save();

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

        string? bestCandidate = null;
        var bestCandidateExpiry = DateTimeOffset.MinValue;

        foreach (var scriptUrl in scriptUrls)
        {
            ct.ThrowIfCancellationRequested();

            var fullUrl = scriptUrl.StartsWith("http") ? scriptUrl : $"{AppleMusicUrl}{scriptUrl}";

            try
            {
                var js = await _httpClient.GetStringAsync(fullUrl, ct);

                foreach (var token in JwtTokenRegex().Matches(js).Select(m => m.Value))
                {
                    if (!IsAppleMusicJwt(token))
                        continue;

                    var expiry = GetJwtExpiry(token);
                    if (expiry is null)
                        continue;

                    if (expiry.Value > bestCandidateExpiry)
                    {
                        bestCandidate = token;
                        bestCandidateExpiry = expiry.Value;
                    }
                }
            }
            catch (HttpRequestException)
            {
                // Skip failed bundles and try next
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // Skip timed out bundles and try next
            }
        }

        if (bestCandidate is not null)
            return bestCandidate;

        throw new InvalidOperationException(
            "Could not extract Apple Music developer token from web player. "
                + "Apple may have changed their web player structure."
        );
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

            if (
                !header.RootElement.TryGetProperty("alg", out var alg)
                || alg.GetString() != "ES256"
                || !header.RootElement.TryGetProperty("kid", out var kid)
                || string.IsNullOrWhiteSpace(kid.GetString())
            )
                return false;

            var payloadJson = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[1]));
            using var payload = JsonDocument.Parse(payloadJson);

            if (
                !payload.RootElement.TryGetProperty("iss", out var iss)
                || string.IsNullOrWhiteSpace(iss.GetString())
                || !payload.RootElement.TryGetProperty("exp", out var exp)
            )
                return false;

            var expiry = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
            return expiry > DateTimeOffset.UtcNow.AddMinutes(5);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (JsonException)
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
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"<script[^>]+src=""([^""]+\.js[^""]*)""")]
    private static partial Regex ScriptSrcRegex();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{20,}\.eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}")]
    private static partial Regex JwtTokenRegex();
}
