using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ciderfy.Web;
using OtpNet;

namespace Ciderfy.Spotify;

/// <summary>
/// Unauthenticated Spotify API client that uses the web player's GraphQL endpoint.
/// </summary>
/// <remarks>
/// Uses the following auth sequence: fetch session info -> obtain access token via TOTP -> get client token
/// </remarks>
internal sealed partial class SpotifyClient(HttpClient httpClient, CookieContainer cookies)
    : IDisposable
{
    private const string UserAgent = HttpClientFactory.SpotifyUserAgent;

    private const string GraphQlEndpoint = "https://api-partner.spotify.com/pathfinder/v2/query";
    private const string ClientTokenEndpoint = "https://clienttoken.spotify.com/v1/clienttoken";
#pragma warning disable S1075
    private const string SpotifyBaseUrl = "https://open.spotify.com";
#pragma warning restore S1075

    private const string PlaylistQueryHash =
        "bb67e0af06e8d6f52b531f97468ee4acd44cd0f82b988e15c2ea47b1148efc77";

    private const string ClientTokenAuthority = "clienttoken.spotify.com";
    private const string SpotifyClientTokenHeader = "Client-Token";
    private const string SpotifyAppVersionHeader = "Spotify-App-Version";

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
    private readonly SemaphoreSlim _authenticationLock = new(1, 1);

    private SpotifyAuthState? _authState;

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
        await EnsureAuthenticatedAsync(ct).ConfigureAwait(false);

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
            )
            .ConfigureAwait(false);

        return ParsePlaylistResponse(response);
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (_authState is not null)
            return;

        await _authenticationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
#pragma warning disable CA1508
            if (_authState is not null)
#pragma warning restore CA1508
                return;

            _authState = await InitializeAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _authenticationLock.Release();
        }
    }

    /// <summary>
    /// Three-step auth: extract client version from the web page,
    /// obtain an access token using a TOTP code, then request a client token
    /// </summary>
    private async Task<SpotifyAuthState> InitializeAsync(CancellationToken ct)
    {
        var session = await GetSessionInfoAsync(ct).ConfigureAwait(false);
        var access = await GetAccessTokenAsync(ct).ConfigureAwait(false);
        var clientToken = await GetClientTokenAsync(
                session.ClientVersion,
                access.ClientId,
                access.DeviceId ?? session.DeviceId,
                ct
            )
            .ConfigureAwait(false);

        return new SpotifyAuthState(access.AccessToken, clientToken, session.ClientVersion);
    }

    private async Task<SessionInfoState> GetSessionInfoAsync(CancellationToken ct)
    {
        using var response = await _httpClient
            .GetAsync(new Uri(SpotifyBaseUrl), ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        string? clientVersion = null;
        var configMatch = AppServerConfigRegex().Match(html);
        if (configMatch.Success)
        {
            var json = Encoding.UTF8.GetString(
                Convert.FromBase64String(configMatch.Groups[1].Value)
            );
            using var doc = JsonDocument.Parse(json);
            clientVersion = doc.RootElement.Deserialize<SessionInfoResponse>()?.ClientVersion;
        }

        return new SessionInfoState(clientVersion, ExtractDeviceIdFromCookies());
    }

    private async Task<AccessTokenState> GetAccessTokenAsync(CancellationToken ct)
    {
        var totpCode = GenerateTotpCode();
        var url =
            $"{SpotifyBaseUrl}/api/token?reason=init&productType=web-player&totp={totpCode}&totpVer={TotpVersion}&totpServer={totpCode}";

        using var response = await _httpClient.GetAsync(new Uri(url), ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var tokenResponse = doc.RootElement.Deserialize<AccessTokenResponse>();
        if (string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
        {
            throw new InvalidOperationException("Spotify access token missing from response.");
        }

        return new AccessTokenState(
            tokenResponse.AccessToken,
            tokenResponse.ClientId,
            ExtractDeviceIdFromCookies()
        );
    }

    private async Task<string> GetClientTokenAsync(
        string? clientVersion,
        string? clientId,
        string? deviceId,
        CancellationToken ct
    )
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                client_data = new
                {
                    client_version = clientVersion,
                    client_id = clientId,
                    js_sdk_data = new
                    {
                        device_brand = "unknown",
                        device_model = "unknown",
                        os = "windows",
                        os_version = "NT 10.0",
                        device_id = deviceId,
                        device_type = "computer",
                    },
                },
            }
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, ClientTokenEndpoint);
        request.Content = new StringContent(payload, Encoding.UTF8);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(MimeTypes.Json);
        request.Headers.Add(HttpHeaderNames.Authority, ClientTokenAuthority);
        request.Headers.Add(HttpHeaderNames.Accept, MimeTypes.Json);
        request.Headers.Add(HttpHeaderNames.UserAgent, UserAgent);

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var clientToken = doc.RootElement.Deserialize<ClientTokenResponse>()?.GrantedToken?.Token;
        if (string.IsNullOrWhiteSpace(clientToken))
            throw new InvalidOperationException("Spotify client token missing from response.");

        return clientToken;
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
        var authState =
            _authState ?? throw new InvalidOperationException("Spotify auth not initialized.");

        var serialized = JsonSerializer.Serialize(
            new
            {
                variables,
                operationName,
                extensions = new { persistedQuery = new { version = 1, sha256Hash = hash } },
            }
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint);
        request.Content = new StringContent(serialized, Encoding.UTF8, MimeTypes.Json);
        request.Headers.Add(HttpHeaderNames.Authorization, $"Bearer {authState.AccessToken}");
        request.Headers.Add(SpotifyClientTokenHeader, authState.ClientToken);
        if (!string.IsNullOrWhiteSpace(authState.ClientVersion))
            request.Headers.Add(SpotifyAppVersionHeader, authState.ClientVersion);

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return JsonDocument.Parse(json);
    }

    private string? ExtractDeviceIdFromCookies()
    {
        var cookieCollection = _cookies.GetCookies(new Uri(SpotifyBaseUrl));
        return cookieCollection["sp_t"]?.Value;
    }

    /// <summary>
    /// Generates a TOTP code from an XOR-obfuscated secret. The secret bytes are
    /// deobfuscated, converted to a Base32 key, and fed into a standard TOTP generator
    /// </summary>
    internal static string GenerateTotpCode()
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

    internal static SpotifyPlaylist ParsePlaylistResponse(JsonDocument doc)
    {
        var tracks = new List<SpotifyTrack>();

        if (
            !doc.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("playlistV2", out var playlist)
        )
        {
            return new SpotifyPlaylist("Spotify Import", tracks);
        }

        var name = playlist.TryGetProperty("name", out var n)
            ? n.GetString() ?? "Spotify Import"
            : "Spotify Import";

        if (
            !playlist.TryGetProperty("content", out var content)
            || !content.TryGetProperty("items", out var items)
        )
        {
            return new SpotifyPlaylist(name, tracks);
        }

        foreach (var item in items.EnumerateArray())
        {
            var track = ParsePlaylistItem(item);
            if (track is not null)
                tracks.Add(track);
        }

        return new SpotifyPlaylist(name, tracks);
    }

    internal static SpotifyTrack? ParsePlaylistItem(JsonElement item)
    {
        if (
            !item.TryGetProperty("itemV2", out var itemV2)
            || !itemV2.TryGetProperty("data", out var itemData)
        )
        {
            return null;
        }

        var title = itemData.TryGetProperty("name", out var tn) ? tn.GetString() : null;
        if (string.IsNullOrWhiteSpace(title))
            return null;

        return new SpotifyTrack
        {
            SpotifyId = ExtractIdFromUri(itemData),
            Title = title,
            Artist = ExtractFirstArtist(itemData),
            DurationMs = ExtractDuration(itemData),
        };
    }

    internal static string ExtractFirstArtist(JsonElement element)
    {
        if (TryGetArtistName(element, "artists", out var name))
            return name;
        if (TryGetArtistName(element, "firstArtist", out name))
            return name;
        return string.Empty;
    }

    internal static bool TryGetArtistName(JsonElement element, string propertyName, out string name)
    {
        name = string.Empty;
        if (
            !element.TryGetProperty(propertyName, out var artists)
            || !artists.TryGetProperty("items", out var items)
            || items.GetArrayLength() == 0
        )
        {
            return false;
        }

        if (
            items[0].TryGetProperty("profile", out var profile)
            && profile.TryGetProperty("name", out var n)
        )
        {
            name = n.GetString() ?? string.Empty;
            return name.Length > 0;
        }

        return false;
    }

    internal static string ExtractIdFromUri(JsonElement element) =>
        element.TryGetProperty("uri", out var uri)
            ? uri.GetString()?.Split(':').LastOrDefault() ?? string.Empty
            : string.Empty;

    internal static int ExtractDuration(JsonElement element)
    {
        if (
            !element.TryGetProperty("trackDuration", out var duration)
            || !duration.TryGetProperty("totalMilliseconds", out var milliseconds)
        )
        {
            return 0;
        }

        return milliseconds.ValueKind switch
        {
            JsonValueKind.Number when milliseconds.TryGetInt32(out var numericMs) => numericMs,
            JsonValueKind.String
                when int.TryParse(
                    milliseconds.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var stringMs
                ) => stringMs,
            _ => 0,
        };
    }

    private sealed record SessionInfoState(string? ClientVersion, string? DeviceId);

    private sealed record AccessTokenState(string AccessToken, string? ClientId, string? DeviceId);

    private sealed record SpotifyAuthState(
        string AccessToken,
        string ClientToken,
        string? ClientVersion
    );

    public void Dispose()
    {
        _authenticationLock.Dispose();
        _httpClient.Dispose();
    }

    [GeneratedRegex(
        @"<script id=""appServerConfig"" type=""text/plain"">([^<]+)</script>",
        RegexOptions.None,
        1000
    )]
    private static partial Regex AppServerConfigRegex();
}

file sealed record SessionInfoResponse(
    [property: JsonPropertyName("clientVersion")] string? ClientVersion
);

file sealed record AccessTokenResponse(
    [property: JsonPropertyName("accessToken")] string? AccessToken,
    [property: JsonPropertyName("clientId")] string? ClientId
);

file sealed record ClientTokenGrantedToken([property: JsonPropertyName("token")] string? Token);

file sealed record ClientTokenResponse(
    [property: JsonPropertyName("granted_token")] ClientTokenGrantedToken? GrantedToken
);
