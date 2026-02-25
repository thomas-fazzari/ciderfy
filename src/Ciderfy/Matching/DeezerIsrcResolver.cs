using System.Text.Json;
using Ciderfy.Configuration.Options;
using Microsoft.Extensions.Options;

namespace Ciderfy.Matching;

/// <summary>
/// Resolves ISRCs for tracks by searching the Deezer catalog
/// </summary>
internal sealed class DeezerIsrcResolver(
    HttpClient httpClient,
    IOptions<DeezerClientOptions> options
) : IDisposable
{
    private const string SearchUrl = "https://api.deezer.com/search";

    private readonly DeezerClientOptions _options = options.Value;
    private readonly SemaphoreSlim _rateLimitLock = new(1, 1);

    private DateTimeOffset _lastCallTime = DateTimeOffset.MinValue;

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
        var results = new TrackMetadata[tracks.Count];
        var completed = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, tracks.Count),
            new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = ct },
            async (i, token) =>
            {
                var track = tracks[i];
                var isrc = await FindIsrcAsync(track.Title, track.Artist, token);
                results[i] = isrc is not null ? track with { Isrc = isrc } : track;

                var currentCount = Interlocked.Increment(ref completed);
                progress?.Report((currentCount, tracks.Count));
            }
        );

        return [.. results];
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
            using var response = await httpClient.GetAsync(url, ct);

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
