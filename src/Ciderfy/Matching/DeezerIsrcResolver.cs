using System.Net;
using System.Text.Json;
using Ciderfy.Configuration.Options;
using Microsoft.Extensions.Options;

namespace Ciderfy.Matching;

/// <summary>
/// Resolves ISRCs for tracks by searching the Deezer catalog
/// </summary>
internal sealed class DeezerIsrcResolver : IDisposable
{
    private const string SearchUrl = "https://api.deezer.com/search";

    private readonly HttpClient _httpClient;
    private readonly DeezerClientOptions _options;
    private readonly SemaphoreSlim _rateLimitLock = new(1, 1);

    private DateTimeOffset _lastCallTime = DateTimeOffset.MinValue;

    public DeezerIsrcResolver(HttpClient httpClient, IOptions<DeezerClientOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    /// <summary>
    /// Resolves ISRCs for a batch of tracks using individual Deezer searches
    /// </summary>
    /// <returns>
    /// The input tracks with their ISRC fields populated where found
    /// </returns>
    public async Task<List<TrackMetadata>> ResolveIsrcsAsync(
        IReadOnlyList<TrackMetadata> tracks,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default
    )
    {
        var results = new List<TrackMetadata>(tracks.Count);

        for (var i = 0; i < tracks.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report((i, tracks.Count));

            var track = tracks[i];
            var isrc = await FindIsrcAsync(track.Title, track.Artist, ct);

            results.Add(isrc is not null ? track with { Isrc = isrc } : track);
        }

        progress?.Report((tracks.Count, tracks.Count));
        return results;
    }

    private async Task<string?> FindIsrcAsync(string title, string artist, CancellationToken ct)
    {
        var query = Uri.EscapeDataString($"{artist} {title}");
        var url = $"{SearchUrl}?q={query}&limit=1";

        var json = await GetWithRateLimitAsync(url, ct);
        if (json is null)
            return null;

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
            return null;

        return data[0].TryGetProperty("isrc", out var isrc) ? isrc.GetString() : null;
    }

    private async Task<string?> GetWithRateLimitAsync(string url, CancellationToken ct)
    {
        await EnforceRateLimitAsync(ct);

        try
        {
            using var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private async Task EnforceRateLimitAsync(CancellationToken ct)
    {
        await _rateLimitLock.WaitAsync(ct);
        try
        {
            var elapsed = (DateTimeOffset.UtcNow - _lastCallTime).TotalMilliseconds;
            if (elapsed < _options.RateLimitDelayMs)
            {
                var delay = _options.RateLimitDelayMs - (int)elapsed;
                await Task.Delay(delay, ct);
            }
            _lastCallTime = DateTimeOffset.UtcNow;
        }
        finally
        {
            _rateLimitLock.Release();
        }
    }

    public void Dispose()
    {
        _rateLimitLock.Dispose();
    }
}
