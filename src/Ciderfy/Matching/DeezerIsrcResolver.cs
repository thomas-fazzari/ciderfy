using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
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

    private readonly RateLimiter _rateLimiter = new SlidingWindowRateLimiter(
        new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 1,
            Window = TimeSpan.FromMilliseconds(options.Value.RateLimitDelayMs),
            SegmentsPerWindow = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue,
        }
    );

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

        var response = JsonSerializer.Deserialize<DeezerSearchResponse>(json);
        return response?.Data is { Count: > 0 } data ? data[0].Isrc : null;
    }

    private async Task<string?> GetWithRateLimitAsync(string url, CancellationToken ct)
    {
        using var lease = await _rateLimiter.AcquireAsync(permitCount: 1, ct);

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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _rateLimiter.Dispose();
    }
}

file sealed record DeezerSearchItem([property: JsonPropertyName("isrc")] string? Isrc);

file sealed record DeezerSearchResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<DeezerSearchItem>? Data
);
