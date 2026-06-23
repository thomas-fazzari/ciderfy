using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Ciderfy.Matching;

/// <summary>
/// Resolves ISRCs for tracks by searching the Deezer catalog.
/// </summary>
internal sealed class DeezerIsrcResolver(
    HttpClient httpClient,
    IOptions<DeezerClientOptions> options
) : IDisposable
{
    private const int SearchResultLimit = 20;
    private const double MinIsrcMatchScore = 0.86;
    private const double MinIsrcMatchMargin = 0.08;
    private const double MinCandidateScore = 0.80;
    private const double AlbumBonusWeight = 0.03;

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
    /// Resolves ISRCs for a batch of tracks using one Deezer search per track.
    /// </summary>
    /// <returns>
    /// The input tracks with their ISRC fields populated where found.
    /// </returns>
    public async Task<List<TrackMetadata>> ResolveIsrcsAsync(
        IReadOnlyList<TrackMetadata> tracks,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default
    )
    {
        var results = new TrackMetadata[tracks.Count];
        var completed = 0;

        await Parallel
            .ForEachAsync(
                Enumerable.Range(0, tracks.Count),
                new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = ct },
                async (i, token) =>
                {
                    var track = tracks[i];
                    var resolution = await FindIsrcsAsync(track, token).ConfigureAwait(false);
                    results[i] =
                        resolution.Candidates.Count > 0
                            ? track with
                            {
                                Isrc = resolution.ConfidentIsrc,
                                IsrcCandidates = resolution.Candidates,
                            }
                            : track;

                    var currentCount = Interlocked.Increment(ref completed);
                    progress?.Report((currentCount, tracks.Count));
                }
            )
            .ConfigureAwait(false);

        return [.. results];
    }

    private async Task<DeezerIsrcResolution> FindIsrcsAsync(
        TrackMetadata track,
        CancellationToken ct
    )
    {
        var sourceTitle = MusicTextNormalizer.NormalizeTitle(track.Title);
        var queryTitle =
            sourceTitle.PrimaryTitle.Length > 0 ? sourceTitle.PrimaryTitle : sourceTitle.Comparable;

        var query = Uri.EscapeDataString($"{track.Artist} {queryTitle}".Trim());

        var url = $"search/track?q={query}&limit={SearchResultLimit}";

        var json = await GetWithRateLimitAsync(url, ct).ConfigureAwait(false);
        if (json is null)
        {
            return DeezerIsrcResolution.Empty;
        }

        var data = ParseSearchItems(json);
        if (data.Count == 0)
        {
            return DeezerIsrcResolution.Empty;
        }

        var scored = data.Select(item => ScoreCandidate(track, sourceTitle, item))
            .Where(score => score is not null)
            .Select(score => score!.Value)
            .GroupBy(score => score.Isrc, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.MaxBy(score => score.Score))
            .OrderByDescending(score => score.Score)
            .ToList();

        if (scored.Count == 0)
        {
            return DeezerIsrcResolution.Empty;
        }

        var best = scored[0];
        if (best.Score < MinIsrcMatchScore)
        {
            return DeezerIsrcResolution.Empty;
        }

        var candidates = scored
            .Where(score => score.Score >= MinCandidateScore)
            .Select(score => score.Isrc)
            .ToList();
        var confidentIsrc =
            scored.Count == 1 || best.Score - scored[1].Score >= MinIsrcMatchMargin
                ? best.Isrc
                : null;

        return new DeezerIsrcResolution(confidentIsrc, candidates);
    }

    private static DeezerCandidateScore? ScoreCandidate(
        TrackMetadata track,
        NormalizedTitle sourceTitle,
        DeezerSearchItem item
    )
    {
        if (string.IsNullOrWhiteSpace(item.Isrc))
        {
            return null;
        }

        var candidateTags = item.ExplicitLyrics ? MusicVersionTag.Explicit : MusicVersionTag.None;
        var candidateTitle = MusicTextNormalizer.NormalizeTitle(
            string.IsNullOrWhiteSpace(item.TitleShort) ? item.Title : item.TitleShort,
            $"{item.Title} {item.TitleVersion}",
            candidateTags
        );
        var versionMultiplier = MusicSimilarity.VersionMultiplier(
            sourceTitle,
            candidateTitle,
            allowNamedVersionMismatch: true
        );

        if (
            versionMultiplier <= 0
            || MusicSimilarity.HasHardDurationMismatch(track.DurationMs, item.DurationMs)
        )
        {
            return null;
        }

        var titleScore = MusicSimilarity.TitleSimilarity(sourceTitle, candidateTitle);
        var artistScore = MusicSimilarity.ArtistSimilarity(track.Artist, item.ArtistName);
        var textScore =
            (titleScore * MatchingWeights.Title) + (artistScore * MatchingWeights.Artist);
        var durationMultiplier = MusicSimilarity.DurationMultiplier(
            track.DurationMs,
            item.DurationMs
        );
        var albumBonus =
            MusicSimilarity.AlbumSimilarity(track.AlbumTitle, item.AlbumTitle) * AlbumBonusWeight;
        var score = Math.Min(
            1.0,
            (textScore * durationMultiplier * versionMultiplier) + albumBonus
        );

        return new DeezerCandidateScore(item.Isrc, score);
    }

    private static List<DeezerSearchItem> ParseSearchItems(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (
                doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array
            )
            {
                return [];
            }

            var items = new List<DeezerSearchItem>(data.GetArrayLength());
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                items.Add(
                    new DeezerSearchItem(
                        Isrc: GetString(item, "isrc"),
                        Title: GetString(item, "title"),
                        TitleShort: GetString(item, "title_short"),
                        TitleVersion: GetString(item, "title_version"),
                        ArtistName: GetNestedString(item, "artist", "name"),
                        AlbumTitle: GetNestedString(item, "album", "title"),
                        DurationMs: GetDurationMs(item),
                        ExplicitLyrics: GetBool(item, "explicit_lyrics")
                    )
                );
            }

            return items;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (
            !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
        )
        {
            return null;
        }

        return property.GetString();
    }

    private static string? GetNestedString(
        JsonElement element,
        string objectName,
        string propertyName
    )
    {
        if (
            !element.TryGetProperty(objectName, out var nested)
            || nested.ValueKind != JsonValueKind.Object
        )
        {
            return null;
        }

        return GetString(nested, propertyName);
    }

    private static int GetDurationMs(JsonElement element)
    {
        if (!element.TryGetProperty("duration", out var duration))
        {
            return 0;
        }

        return duration.ValueKind switch
        {
            JsonValueKind.Number when duration.TryGetInt32(out var seconds) => seconds * 1000,
            JsonValueKind.String
                when int.TryParse(
                    duration.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var seconds
                ) => seconds * 1000,
            _ => 0,
        };
    }

    private static bool GetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            _ => false,
        };
    }

    private async Task<string?> GetWithRateLimitAsync(string url, CancellationToken ct)
    {
        using var lease = await _rateLimiter.AcquireAsync(permitCount: 1, ct).ConfigureAwait(false);

        try
        {
            using var response = await httpClient
                .GetAsync(new Uri(url, UriKind.Relative), ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
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
        httpClient.Dispose();
        _rateLimiter.Dispose();
    }

    private readonly record struct DeezerCandidateScore(string Isrc, double Score);

    private readonly record struct DeezerIsrcResolution(
        string? ConfidentIsrc,
        IReadOnlyList<string> Candidates
    )
    {
        internal static DeezerIsrcResolution Empty { get; } = new(null, []);
    }

    private sealed record DeezerSearchItem(
        string? Isrc,
        string? Title,
        string? TitleShort,
        string? TitleVersion,
        string? ArtistName,
        string? AlbumTitle,
        int DurationMs,
        bool ExplicitLyrics
    );
}
