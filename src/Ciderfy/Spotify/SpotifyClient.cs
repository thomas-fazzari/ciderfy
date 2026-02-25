using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ciderfy.Matching;
using OtpNet;

namespace Ciderfy.Spotify;

/// <summary>
/// Unauthenticated Spotify API client that uses the web player's GraphQL endpoint.
/// </summary>
/// <remarks>
/// Uses the following auth sequence: fetch session info -> obtain access token via TOTP -> get client token
/// </remarks>
internal sealed partial class SpotifyClient(HttpClient httpClient, CookieContainer cookies)
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string GraphQlEndpoint = "https://api-partner.spotify.com/pathfinder/v2/query";
    private const string ClientTokenEndpoint = "https://clienttoken.spotify.com/v1/clienttoken";
    private const string SpotifyBaseUrl = "https://open.spotify.com";

    private const string PlaylistQueryHash =
        "bb67e0af06e8d6f52b531f97468ee4acd44cd0f82b988e15c2ea47b1148efc77";

    private const int TotpVersion = 61;

    // csharpier-ignore-start
    private static readonly byte[] _totpSecret =
    [
        44, 55, 47, 42, 70, 40, 34, 114, 76, 74, 50, 111,
        120, 97, 75, 76, 94, 102, 43, 69, 49, 120, 118, 80, 64, 78,
    ];
    // csharpier-ignore-end

    private readonly HttpClient _httpClient = httpClient;
    private readonly CookieContainer _cookies = cookies;

    private string? _accessToken;
    private string? _clientToken;
    private string? _clientId;
    private string? _clientVersion;
    private string? _deviceId;

    /// <summary>
    /// Fetches a playlist's metadata and tracks from Spotify using the unauthenticated GraphQL API
    /// </summary>
    /// <remarks>
    /// Limited to 1000 tracks per request (no pagination)
    /// </remarks>
    public async Task<SpotifyPlaylist> GetPlaylistAsync(
        string playlistId,
        CancellationToken ct = default
    )
    {
        await EnsureAuthenticatedAsync(ct);

        var variables = new
        {
            uri = $"spotify:playlist:{playlistId}",
            offset = 0,
            limit = 1000,
            enableWatchFeedEntrypoint = false,
        };
        using var response = await QueryGraphQlAsync(
            PlaylistQueryHash,
            "fetchPlaylist",
            variables,
            ct
        );

        return ParsePlaylistResponse(response);
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (_accessToken is null)
            await InitializeAsync(ct);
    }

    /// <summary>
    /// Three-step auth: extract client version from the web page,
    /// obtain an access token using a TOTP code, then request a client token
    /// </summary>
    private async Task InitializeAsync(CancellationToken ct)
    {
        await GetSessionInfoAsync(ct);
        await GetAccessTokenAsync(ct);
        await GetClientTokenAsync(ct);
    }

    private async Task GetSessionInfoAsync(CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(SpotifyBaseUrl, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);

        var configMatch = AppServerConfigRegex().Match(html);
        if (configMatch.Success)
        {
            var json = Encoding.UTF8.GetString(
                Convert.FromBase64String(configMatch.Groups[1].Value)
            );
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("clientVersion", out var version))
                _clientVersion = version.GetString();
        }

        ExtractDeviceIdFromCookies();
    }

    private async Task GetAccessTokenAsync(CancellationToken ct)
    {
        var totpCode = GenerateTotpCode();
        var url =
            $"{SpotifyBaseUrl}/api/token?reason=init&productType=web-player&totp={totpCode}&totpVer={TotpVersion}&totpServer={totpCode}";

        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        _accessToken = doc.RootElement.GetProperty("accessToken").GetString();
        _clientId = doc.RootElement.GetProperty("clientId").GetString();

        ExtractDeviceIdFromCookies();
    }

    private async Task GetClientTokenAsync(CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                client_data = new
                {
                    client_version = _clientVersion,
                    client_id = _clientId,
                    js_sdk_data = new
                    {
                        device_brand = "unknown",
                        device_model = "unknown",
                        os = "windows",
                        os_version = "NT 10.0",
                        device_id = _deviceId,
                        device_type = "computer",
                    },
                },
            }
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, ClientTokenEndpoint);
        request.Content = new StringContent(payload, Encoding.UTF8);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("Authority", "clienttoken.spotify.com");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("User-Agent", UserAgent);

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (
            doc.RootElement.TryGetProperty("granted_token", out var grantedToken)
            && grantedToken.TryGetProperty("token", out var token)
        )
        {
            _clientToken = token.GetString();
        }
    }

    /// <summary>
    /// Sends a persisted GraphQL query identified by its SHA-256 hash (no dynamic query strings)
    /// </summary>
    private async Task<JsonDocument> QueryGraphQlAsync(
        string hash,
        string operationName,
        object variables,
        CancellationToken ct
    )
    {
        var serialized = JsonSerializer.Serialize(
            new
            {
                variables,
                operationName,
                extensions = new { persistedQuery = new { version = 1, sha256Hash = hash } },
            }
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint);
        request.Content = new StringContent(serialized, Encoding.UTF8, "application/json");
        request.Headers.Add("Authorization", $"Bearer {_accessToken}");
        request.Headers.Add("Client-Token", _clientToken);
        request.Headers.Add("Spotify-App-Version", _clientVersion);

        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(json);
    }

    private void ExtractDeviceIdFromCookies()
    {
        var cookieCollection = _cookies.GetCookies(new Uri(SpotifyBaseUrl));
        if (cookieCollection["sp_t"] is { } spTCookie)
            _deviceId = spTCookie.Value;
    }

    /// <summary>
    /// Generates a TOTP code from an XOR-obfuscated secret. The secret bytes are
    /// deobfuscated, converted to a Base32 key, and fed into a standard TOTP generator
    /// </summary>
    private static string GenerateTotpCode()
    {
        Span<byte> transformed = stackalloc byte[_totpSecret.Length];
        for (var i = 0; i < _totpSecret.Length; i++)
            transformed[i] = (byte)(_totpSecret[i] ^ ((i % 33) + 9));

        var sb = new StringBuilder(transformed.Length * 3);
        foreach (var b in transformed)
            sb.Append(b);

        var key = Encoding.UTF8.GetBytes(sb.ToString());
        return new Totp(key, step: 30).ComputeTotp();
    }

    private static SpotifyPlaylist ParsePlaylistResponse(JsonDocument doc)
    {
        var tracks = new List<TrackMetadata>();

        if (
            !doc.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("playlistV2", out var playlist)
        )
            return new SpotifyPlaylist("Spotify Import", tracks);

        var name = playlist.TryGetProperty("name", out var n)
            ? n.GetString() ?? "Spotify Import"
            : "Spotify Import";

        if (
            !playlist.TryGetProperty("content", out var content)
            || !content.TryGetProperty("items", out var items)
        )
            return new SpotifyPlaylist(name, tracks);

        foreach (var item in items.EnumerateArray())
        {
            var track = ParsePlaylistItem(item);
            if (track is not null)
                tracks.Add(track);
        }

        return new SpotifyPlaylist(name, tracks);
    }

    private static TrackMetadata? ParsePlaylistItem(JsonElement item)
    {
        if (
            !item.TryGetProperty("itemV2", out var itemV2)
            || !itemV2.TryGetProperty("data", out var itemData)
        )
            return null;

        var title = itemData.TryGetProperty("name", out var tn) ? tn.GetString() : null;
        if (string.IsNullOrWhiteSpace(title))
            return null;

        return new TrackMetadata
        {
            SpotifyId = ExtractIdFromUri(itemData),
            Title = title,
            Artist = ExtractFirstArtist(itemData),
            DurationMs = ExtractDuration(itemData),
        };
    }

    private static string ExtractFirstArtist(JsonElement element)
    {
        if (TryGetArtistName(element, "artists", out var name))
            return name;
        if (TryGetArtistName(element, "firstArtist", out name))
            return name;
        return "";
    }

    private static bool TryGetArtistName(JsonElement element, string propertyName, out string name)
    {
        name = "";
        if (
            !element.TryGetProperty(propertyName, out var artists)
            || !artists.TryGetProperty("items", out var items)
            || items.GetArrayLength() == 0
        )
            return false;

        if (
            items[0].TryGetProperty("profile", out var profile)
            && profile.TryGetProperty("name", out var n)
        )
        {
            name = n.GetString() ?? "";
            return name.Length > 0;
        }

        return false;
    }

    private static string ExtractIdFromUri(JsonElement element) =>
        element.TryGetProperty("uri", out var uri)
            ? uri.GetString()?.Split(':').LastOrDefault() ?? ""
            : "";

    private static int ExtractDuration(JsonElement element)
    {
        if (
            !element.TryGetProperty("trackDuration", out var duration)
            || !duration.TryGetProperty("totalMilliseconds", out var milliseconds)
        )
            return 0;

        return milliseconds.ValueKind switch
        {
            JsonValueKind.Number when milliseconds.TryGetInt32(out var numericMs) => numericMs,
            JsonValueKind.String when int.TryParse(milliseconds.GetString(), out var stringMs) =>
                stringMs,
            _ => 0,
        };
    }

    [GeneratedRegex(@"<script id=""appServerConfig"" type=""text/plain"">([^<]+)</script>")]
    private static partial Regex AppServerConfigRegex();
}
