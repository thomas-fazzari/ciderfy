using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Ciderfy.Apple;

public sealed class AppleMusicRateLimitException() : Exception("Apple Music rate limit exhausted");

public sealed class AppleMusicUnauthorizedException()
    : Exception("Apple Music returned 401 Unauthorized, developer token may have expired");

internal sealed class AppleMusicClient : IDisposable
{
    private const string ApiBaseUrl = "https://api.music.apple.com/v1";
    private const int MinDelayBetweenCallsMs = 1000;

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _rateLimitLock = new(1, 1);

    private string? _userToken;
    private DateTimeOffset _lastCallTime = DateTimeOffset.MinValue;

    public AppleMusicClient()
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };

        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        _httpClient.DefaultRequestHeaders.Add(
            "User-Agent",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)"
        );
        _httpClient.DefaultRequestHeaders.Add("Origin", "https://music.apple.com");
    }

    public void SetTokens(string developerToken, string? userToken = null)
    {
        _userToken = userToken;

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            developerToken
        );

        if (userToken is not null)
        {
            _httpClient.DefaultRequestHeaders.Remove("Music-User-Token");
            _httpClient.DefaultRequestHeaders.Add("Music-User-Token", userToken);
        }
    }

    public async Task<Dictionary<string, AppleMusicTrack>> BatchSearchByIsrcAsync(
        IReadOnlyList<string> isrcs,
        string storefront = "us",
        CancellationToken ct = default
    )
    {
        const int batchSize = 25;
        var result = new Dictionary<string, AppleMusicTrack>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < isrcs.Count; i += batchSize)
        {
            ct.ThrowIfCancellationRequested();

            var end = Math.Min(i + batchSize, isrcs.Count);
            var joined = string.Join(',', isrcs.Take(new Range(i, end)));
            var url = $"{ApiBaseUrl}/catalog/{storefront}/songs?filter[isrc]={joined}";
            var json = await GetWithRateLimitAsync(url, ct);

            if (json is null)
                continue;

            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("data", out var data))
                continue;

            foreach (var item in data.EnumerateArray())
            {
                var track = ParseTrack(item);
                if (track?.Isrc is not null)
                    result.TryAdd(track.Isrc, track);
            }
        }

        return result;
    }

    public async Task<List<AppleMusicTrack>> SearchByTextAllAsync(
        string query,
        string storefront = "us",
        CancellationToken ct = default
    )
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var url =
            $"{ApiBaseUrl}/catalog/{storefront}/search?types=songs&limit=10&term={encodedQuery}";
        var json = await GetWithRateLimitAsync(url, ct);
        var tracks = new List<AppleMusicTrack>();

        if (json is null)
            return tracks;

        using var doc = JsonDocument.Parse(json);

        if (
            !doc.RootElement.TryGetProperty("results", out var results)
            || !results.TryGetProperty("songs", out var songs)
            || !songs.TryGetProperty("data", out var data)
        )
            return tracks;

        foreach (var item in data.EnumerateArray())
        {
            var track = ParseTrack(item);
            if (track is not null)
                tracks.Add(track);
        }

        return tracks;
    }

    public async Task<string?> CreatePlaylistAsync(
        string name,
        string? description = null,
        CancellationToken ct = default
    )
    {
        EnsureUserToken();

        var payload = new
        {
            attributes = new
            {
                name,
                description = description ?? "Imported from Spotify using Ciderfy",
            },
        };

        var json = JsonSerializer.Serialize(payload);
        var url = $"{ApiBaseUrl}/me/library/playlists";

        var responseJson = await PostWithRateLimitAsync(url, json, ct);

        if (responseJson is null)
            return null;

        using var doc = JsonDocument.Parse(responseJson);

        if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
            return data[0].TryGetProperty("id", out var id) ? id.GetString() : null;

        return null;
    }

    public async Task<bool> AddTracksToPlaylistAsync(
        string playlistId,
        IReadOnlyList<string> trackIds,
        CancellationToken ct = default
    )
    {
        EnsureUserToken();

        const int batchSize = 100;

        for (var i = 0; i < trackIds.Count; i += batchSize)
        {
            ct.ThrowIfCancellationRequested();

            var end = Math.Min(i + batchSize, trackIds.Count);
            var batch = trackIds.Take(new Range(i, end));
            var payload = new { data = batch.Select(id => new { id, type = "songs" }).ToArray() };

            var json = JsonSerializer.Serialize(payload);
            var url = $"{ApiBaseUrl}/me/library/playlists/{playlistId}/tracks";

            var response = await PostWithRateLimitAsync(url, json, ct);
            if (response is null)
                return false;
        }

        return true;
    }

    private void EnsureUserToken()
    {
        if (string.IsNullOrEmpty(_userToken))
            throw new InvalidOperationException(
                "User token is required for this operation. Run '/auth' first."
            );
    }

    private Task<string?> GetWithRateLimitAsync(string url, CancellationToken ct) =>
        SendAsync(token => _httpClient.GetAsync(url, token), ct);

    private Task<string?> PostWithRateLimitAsync(
        string url,
        string jsonBody,
        CancellationToken ct
    ) =>
        SendAsync(
            async token =>
            {
                using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                return await _httpClient.PostAsync(url, content, token);
            },
            ct
        );

    private async Task<string?> SendAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> sendAsync,
        CancellationToken ct
    )
    {
        await EnforceRateLimitAsync(ct);
        ct.ThrowIfCancellationRequested();

        using var response = await sendAsync(ct);

        if (response.StatusCode is HttpStatusCode.TooManyRequests)
            throw new AppleMusicRateLimitException();

        if (response.StatusCode is HttpStatusCode.Unauthorized)
            throw new AppleMusicUnauthorizedException();

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task EnforceRateLimitAsync(CancellationToken ct)
    {
        await _rateLimitLock.WaitAsync(ct);
        try
        {
            var elapsed = (DateTimeOffset.UtcNow - _lastCallTime).TotalMilliseconds;
            if (elapsed < MinDelayBetweenCallsMs)
                await Task.Delay(MinDelayBetweenCallsMs - (int)elapsed, ct);

            _lastCallTime = DateTimeOffset.UtcNow;
        }
        finally
        {
            _rateLimitLock.Release();
        }
    }

    private static AppleMusicTrack? ParseTrack(JsonElement element)
    {
        if (
            !element.TryGetProperty("id", out var id)
            || !element.TryGetProperty("attributes", out var attrs)
        )
            return null;

        return new AppleMusicTrack
        {
            Id = id.GetString() ?? "",
            Title = attrs.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            Artist = attrs.TryGetProperty("artistName", out var artist)
                ? artist.GetString() ?? ""
                : "",
            DurationMs = attrs.TryGetProperty("durationInMillis", out var dur) ? dur.GetInt32() : 0,
            Isrc = attrs.TryGetProperty("isrc", out var isrc) ? isrc.GetString() : null,
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _rateLimitLock.Dispose();
    }
}
